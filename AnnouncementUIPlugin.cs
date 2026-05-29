using HarmonyLib;
using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Core.Utils;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.NetTransport;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace AnnounceUI
{
    public class AnnouncementUIPlugin : RocketPlugin<AnnouncementUIConfiguration>
    {
        public static AnnouncementUIPlugin Instance { get; private set; }

        private const short EFFECT_KEY = 11451;

        private readonly Dictionary<ulong, PlayerUIState> _states = new Dictionary<ulong, PlayerUIState>();

        private Timer _timer;
        private Timer _overrideTimer;
        private Timer _uiCheckTimer;
        private int _announceIndex;
        private string _currentAnnouncementRaw;

        private bool _overrideActive;
        private DateTime _overrideUntilUtc;
        private string _overrideMessageRaw;

        private Harmony _harmony;

        // ── 缓存，避免重复分配 ──
        private static readonly Color DefaultColor = Color.white;
        private static readonly Color YellowColor = Color.yellow;

        public override TranslationList DefaultTranslations => new TranslationList()
        {
            { "annui_all_on", "AnnUI: 已开启全部UI" },
            { "annui_all_off", "AnnUI: 已关闭全部UI" },
            { "annui_rules_on", "AnnUI: 已开启 服务器规则UI (01)" },
            { "annui_rules_off", "AnnUI: 已关闭 服务器规则UI (01)" },
            { "annui_announce_on", "AnnUI: 已开启 服务器公告UI (02)" },
            { "annui_announce_off", "AnnUI: 已关闭 服务器公告UI (02)" },
            { "annui_usage", "用法: /ANNUI | /ANNUI 01 | /ANNUI 02" },
            { "annui_all_off_hint", "AnnUI: 总开关已关闭，请先使用 /ANNUI 开启总开关" },
            { "ann_usage", "用法: /ann <时间(秒)> <文字>" },
            { "ann_time_invalid", "时间必须是数字(秒)。示例: /ann 60 维护中" },
            { "ann_sent", "已强制广播 {0} 秒。" },
            { "no_plugin_instance", "插件未加载或实例为空，请重启服务器。" },
        };

        // ══════════════════════════════════════════════
        //  生命周期
        // ══════════════════════════════════════════════

        protected override void Load()
        {
            Instance = this;

            Logger.Log("========================================", ConsoleColor.Cyan);
            Logger.Log("[AnnounceUI] 插件制作人：欧小茶", ConsoleColor.Cyan);
            Logger.Log("[AnnounceUI] 联系方式：QQ56896686", ConsoleColor.Cyan);
            Logger.Log("[AnnounceUI] 插件已成功加载！！", ConsoleColor.Green);
            Logger.Log("========================================", ConsoleColor.Cyan);

            U.Events.OnPlayerConnected += OnPlayerConnected;
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
            PlayerAnimator.OnGestureChanged_Global += OnGestureChanged;
            PlayerLife.onPlayerDied += HandlePlayerDied;

            StartAnnouncerTimer();
            StartUICheckTimer();
            PluginDocsGenerator.WriteDocs(this);

            _harmony = new Harmony("com.oxc.announceui");
            _harmony.PatchAll();

            Logger.Log($"[AnnounceUI] EffectId={Configuration.Instance.EffectId}, Key={EFFECT_KEY}", ConsoleColor.Yellow);
            Logger.Log($"[AnnounceUI] ChatAvatarURL: {Configuration.Instance.ChatAvatarURL}", ConsoleColor.Yellow);
            Logger.Log("[AnnounceUI] Harmony + Event detection ready.", ConsoleColor.Yellow);
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= OnPlayerConnected;
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;
            PlayerAnimator.OnGestureChanged_Global -= OnGestureChanged;
            PlayerLife.onPlayerDied -= HandlePlayerDied;

            StopAnnouncerTimer();
            StopOverrideTimer();
            StopUICheckTimer();

            ClearUIForAllOnlinePlayers();

            _harmony?.UnpatchAll("com.oxc.announceui");
            _harmony = null;

            _states.Clear();
            Instance = null;

            Logger.Log("[AnnounceUI] Unloaded.", ConsoleColor.Yellow);
        }

        // ══════════════════════════════════════════════
        //  玩家连接 / 断开
        // ══════════════════════════════════════════════

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            var state = GetOrCreateState(player, resetToDefault: true);

            var conn = GetConnection(player);
            if (conn == null) return;

            SendEffectIfNeeded(conn, state);
            UpdateStaticTexts(player, conn, state);
            UpdateAnnouncementText(player, conn, state, IsOverrideActive());
            ApplyVisibility(player, conn, state);
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            _states.Remove(player.CSteamID.m_SteamID);
        }

        // ══════════════════════════════════════════════
        //  手势事件 — 背包 / 投降 / 休息 / 举手
        // ══════════════════════════════════════════════

        private void OnGestureChanged(PlayerAnimator animator, EPlayerGesture gesture)
        {
            try
            {
                if (animator?.player == null) return;

                var uPlayer = UnturnedPlayer.FromPlayer(animator.player);
                if (uPlayer == null) return;

                ulong sid = uPlayer.CSteamID.m_SteamID;
                if (!_states.TryGetValue(sid, out var state)) return;

                switch (gesture)
                {
                    case EPlayerGesture.INVENTORY_START:
                        state.IsInventoryOpen = true;
                        HideIfNeeded(uPlayer, state);
                        break;

                    case EPlayerGesture.INVENTORY_STOP:
                        state.IsInventoryOpen = false;
                        TryRestoreIfNoBlockers(uPlayer, state);
                        break;

                    case EPlayerGesture.SURRENDER_STOP:
                    case EPlayerGesture.REST_STOP:
                    case EPlayerGesture.ARREST_STOP:
                        TryRestoreIfNoBlockers(uPlayer, state);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[AnnounceUI] OnGestureChanged error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════
        //  死亡事件
        // ══════════════════════════════════════════════

        private void HandlePlayerDied(PlayerLife life, EDeathCause cause, ELimb limb, CSteamID instigator)
        {
            try
            {
                if (life?.player == null) return;

                var uPlayer = UnturnedPlayer.FromPlayer(life.player);
                if (uPlayer == null) return;

                TaskDispatcher.QueueOnMainThread(() =>
                {
                    if (!_states.TryGetValue(uPlayer.CSteamID.m_SteamID, out var state)) return;
                    HideIfNeeded(uPlayer, state);
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[AnnounceUI] HandlePlayerDied error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════
        //  Harmony 补丁
        // ══════════════════════════════════════════════

        [HarmonyPatch(typeof(PlayerInventory), "openStorage")]
        internal static class OpenStoragePatch
        {
            [HarmonyPrefix]
            private static void Prefix(PlayerInventory __instance)
            {
                TryHideForPlayer(__instance.player);
            }
        }

        [HarmonyPatch(typeof(PlayerInventory), "closeStorage")]
        internal static class CloseStoragePatch
        {
            [HarmonyPrefix]
            private static void Prefix() { }
        }

        [HarmonyPatch(typeof(PlayerInventory), "openTrunk")]
        internal static class OpenTrunkPatch
        {
            [HarmonyPrefix]
            private static void Prefix(PlayerInventory __instance)
            {
                TryHideForPlayer(__instance.player);
            }
        }

        [HarmonyPatch(typeof(PlayerInventory), "closeTrunk")]
        internal static class CloseTrunkPatch
        {
            [HarmonyPrefix]
            private static void Prefix() { }
        }

        [HarmonyPatch(typeof(PlayerInventory), "closeDistantStorage")]
        internal static class CloseDistantStoragePatch
        {
            [HarmonyPrefix]
            private static void Prefix() { }
        }

        /// <summary>
        /// 拦截手势请求，覆盖 OnGestureChanged_Global 未处理的手势类型
        /// </summary>
        [HarmonyPatch(typeof(PlayerAnimator), "ReceiveGestureRequest")]
        internal static class ReceiveGesturePatch
        {
            [HarmonyPrefix]
            private static void Prefix(PlayerAnimator __instance, EPlayerGesture newGesture)
            {
                try
                {
                    if (__instance?.player == null) return;

                    // 只处理事件未覆盖的手势，INVENTORY_START 由 OnGestureChanged 处理
                    if (newGesture != EPlayerGesture.SURRENDER_START
                        && newGesture != EPlayerGesture.REST_START
                        && newGesture != EPlayerGesture.ARREST_START)
                        return;

                    var uPlayer = UnturnedPlayer.FromPlayer(__instance.player);
                    if (uPlayer == null) return;

                    var plugin = Instance;
                    if (plugin == null) return;

                    ulong sid = uPlayer.CSteamID.m_SteamID;
                    if (!plugin._states.TryGetValue(sid, out var state)) return;

                    plugin.HideIfNeeded(uPlayer, state);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[AnnounceUI] ReceiveGesturePatch error: {ex.Message}");
                }
            }
        }

        private static void TryHideForPlayer(Player player)
        {
            try
            {
                var plugin = Instance;
                if (plugin == null) return;

                var uPlayer = UnturnedPlayer.FromPlayer(player);
                if (uPlayer == null) return;

                ulong sid = uPlayer.CSteamID.m_SteamID;
                if (!plugin._states.TryGetValue(sid, out var state)) return;

                plugin.HideIfNeeded(uPlayer, state);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[AnnounceUI] TryHideForPlayer error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════
        //  轮询定时器 — 储物箱 / 后备箱 / 死亡
        // ══════════════════════════════════════════════

        private void StartUICheckTimer()
        {
            StopUICheckTimer();
            _uiCheckTimer = new Timer(1000);
            _uiCheckTimer.Elapsed += OnUICheckElapsed;
            _uiCheckTimer.AutoReset = true;
            _uiCheckTimer.Start();
        }

        private void StopUICheckTimer()
        {
            if (_uiCheckTimer != null)
            {
                _uiCheckTimer.Stop();
                _uiCheckTimer.Dispose();
                _uiCheckTimer = null;
            }
        }

        private void OnUICheckElapsed(object sender, ElapsedEventArgs e)
        {
            TaskDispatcher.QueueOnMainThread(PollPlayerStates);
        }

        private void PollPlayerStates()
        {
            var clients = Provider.clients;
            for (int i = 0; i < clients.Count; i++)
            {
                try
                {
                    var sp = clients[i];
                    var player = sp.player;
                    if (player == null) continue;

                    ulong sid = sp.playerID.steamID.m_SteamID;
                    if (!_states.TryGetValue(sid, out var state)) continue;

                    bool isStoring = player.inventory.isStoring;
                    bool isDead = player.life.isDead;
                    bool isTrunk = player.inventory.isStorageTrunk;
                    bool anyBlocker = isStoring || isDead || isTrunk;

                    if (anyBlocker && !state.IsHiddenByGameUI)
                    {
                        state.IsHiddenByGameUI = true;
                        var conn = GetConnection(player);
                        if (conn != null)
                            EffectManager.sendUIEffectVisibility(EFFECT_KEY, conn, true, "Canvas", false);
                    }
                    else if (!anyBlocker && !state.IsInventoryOpen && state.IsHiddenByGameUI)
                    {
                        state.IsHiddenByGameUI = false;
                        var uPlayer = UnturnedPlayer.FromPlayer(player);
                        if (uPlayer != null)
                            RestoreUIForPlayer(uPlayer, state);
                    }
                }
                catch { }
            }
        }

        // ══════════════════════════════════════════════
        //  UI 隐藏 / 恢复
        // ══════════════════════════════════════════════

        private void HideIfNeeded(UnturnedPlayer player, PlayerUIState state)
        {
            if (state.IsHiddenByGameUI) return;

            state.IsHiddenByGameUI = true;

            var conn = GetConnection(player);
            if (conn != null)
                EffectManager.sendUIEffectVisibility(EFFECT_KEY, conn, true, "Canvas", false);
        }

        private void TryRestoreIfNoBlockers(UnturnedPlayer player, PlayerUIState state)
        {
            if (!state.IsHiddenByGameUI) return;

            var p = player.Player;
            if (p != null && (p.inventory.isStoring || p.life.isDead || p.inventory.isStorageTrunk))
                return;

            state.IsHiddenByGameUI = false;
            RestoreUIForPlayer(player, state);
        }

        private void RestoreUIForPlayer(UnturnedPlayer player, PlayerUIState state)
        {
            var conn = GetConnection(player);
            if (conn == null) return;

            SendEffectIfNeeded(conn, state);
            UpdateStaticTexts(player, conn, state);
            UpdateAnnouncementText(player, conn, state, IsOverrideActive());
            ApplyVisibility(player, conn, state);
        }

        // ══════════════════════════════════════════════
        //  UI 可见性
        // ══════════════════════════════════════════════

        private void ApplyVisibility(UnturnedPlayer player, ITransportConnection conn, PlayerUIState state)
        {
            if (state.IsHiddenByGameUI)
            {
                EffectManager.sendUIEffectVisibility(EFFECT_KEY, conn, true, "Canvas", false);
                return;
            }

            var cfg = Configuration.Instance;
            bool showRules = state.AllEnabled && state.RulesEnabled && cfg.EnableRulesUI;
            bool showAnn = state.AllEnabled && state.AnnouncementsEnabled && cfg.EnableUIAnnouncements;
            bool showCanvas = state.AllEnabled && (showRules || showAnn);

            EffectManager.sendUIEffectVisibility(EFFECT_KEY, conn, true, "Canvas", showCanvas);
            EffectManager.sendUIEffectVisibility(EFFECT_KEY, conn, true, "ServerRulesUI", showRules);
            EffectManager.sendUIEffectVisibility(EFFECT_KEY, conn, true, "AnnouncementUI", showAnn);

            if (showAnn)
                UpdateAnnouncementText(player, conn, state, IsOverrideActive());
        }

        // ══════════════════════════════════════════════
        //  文本更新
        // ══════════════════════════════════════════════

        private void UpdateStaticTexts(UnturnedPlayer player, ITransportConnection conn, PlayerUIState state)
        {
            var cfg = Configuration.Instance;
            if (!cfg.EnableRulesUI || !state.AllEnabled || !state.RulesEnabled) return;

            SendEffectIfNeeded(conn, state);

            EffectManager.sendUIEffectText(EFFECT_KEY, conn, true, "ServerText",
                TextFormatter.Format(cfg.ServerTitleText ?? "服务器规则", player));

            var rules = cfg.Rules;
            int count = cfg.RulesFieldCount;
            for (int i = 1; i <= count; i++)
            {
                string elementName = (i == 1) ? "ServerRulesText" : ("ServerRulesText" + (i - 1));
                string raw = (rules != null && i <= rules.Count) ? rules[i - 1] : string.Empty;
                EffectManager.sendUIEffectText(EFFECT_KEY, conn, true, elementName,
                    TextFormatter.Format(raw, player));
            }

            EffectManager.sendUIEffectText(EFFECT_KEY, conn, true, "HelpText",
                TextFormatter.Format(cfg.HelpText ?? string.Empty, player));
        }

        private void UpdateAnnouncementText(UnturnedPlayer player, ITransportConnection conn, PlayerUIState state, bool forceOverride)
        {
            var cfg = Configuration.Instance;
            if (!cfg.EnableUIAnnouncements || !state.AllEnabled || !state.AnnouncementsEnabled) return;

            string raw = forceOverride ? (_overrideMessageRaw ?? string.Empty) : GetCurrentAnnouncementRaw();
            EffectManager.sendUIEffectText(EFFECT_KEY, conn, true, "AnnouncementText",
                TextFormatter.Format(raw, player));
        }

        // ══════════════════════════════════════════════
        //  Effect 发送
        // ══════════════════════════════════════════════

        private void SendEffectIfNeeded(ITransportConnection conn, PlayerUIState state)
        {
            if (state.EffectSent) return;

            EffectManager.sendUIEffect(Configuration.Instance.EffectId, EFFECT_KEY, conn, true);
            state.EffectSent = true;
        }

        // ══════════════════════════════════════════════
        //  广播定时器
        // ══════════════════════════════════════════════

        private void StartAnnouncerTimer()
        {
            StopAnnouncerTimer();

            double interval = Configuration.Instance.AnnouncementIntervalSeconds;
            if (interval <= 0) return;

            _timer = new Timer(interval * 1000);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }

        private void StopAnnouncerTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            TaskDispatcher.QueueOnMainThread(() =>
            {
                if (_overrideActive && DateTime.UtcNow >= _overrideUntilUtc)
                {
                    _overrideActive = false;
                    _overrideMessageRaw = null;
                }

                var announcements = Configuration.Instance.Announcements;
                if (announcements == null || announcements.Count == 0) return;

                _announceIndex = (_announceIndex + 1) % announcements.Count;
                _currentAnnouncementRaw = announcements[_announceIndex];

                BroadcastAnnouncement(_overrideActive);
            });
        }

        // ══════════════════════════════════════════════
        //  强制广播
        // ══════════════════════════════════════════════

        public void StartForcedAnnouncement(string rawMessage, int durationSeconds)
        {
            if (durationSeconds <= 0) durationSeconds = 60;

            TaskDispatcher.QueueOnMainThread(() =>
            {
                StopOverrideTimer();

                _overrideActive = true;
                _overrideMessageRaw = rawMessage ?? string.Empty;
                _overrideUntilUtc = DateTime.UtcNow.AddSeconds(durationSeconds);

                BroadcastAnnouncement(forceOverride: true);

                var t = new Timer(Math.Max(1, durationSeconds) * 1000d);
                t.AutoReset = false;
                t.Elapsed += (s, ev) =>
                {
                    TaskDispatcher.QueueOnMainThread(() =>
                    {
                        _overrideActive = false;
                        _overrideMessageRaw = null;
                        _overrideTimer = null;
                        BroadcastAnnouncement(forceOverride: false);
                        t.Dispose();
                    });
                };
                _overrideTimer = t;
                t.Start();
            });
        }

        private void StopOverrideTimer()
        {
            if (_overrideTimer != null)
            {
                _overrideTimer.Stop();
                _overrideTimer.Dispose();
                _overrideTimer = null;
            }
        }

        // ══════════════════════════════════════════════
        //  广播
        // ══════════════════════════════════════════════

        private void BroadcastAnnouncement(bool forceOverride)
        {
            string raw = forceOverride ? _overrideMessageRaw : GetCurrentAnnouncementRaw();
            if (string.IsNullOrEmpty(raw)) return;

            bool enableChat = Configuration.Instance.EnableChatAnnouncements;
            var clients = Provider.clients;

            for (int i = 0; i < clients.Count; i++)
            {
                var sp = clients[i];
                var player = UnturnedPlayer.FromSteamPlayer(sp);
                if (player == null) continue;

                ulong sid = player.CSteamID.m_SteamID;
                if (!_states.TryGetValue(sid, out var state)) continue;

                var conn = GetConnection(player);
                if (conn == null) continue;

                UpdateAnnouncementText(player, conn, state, forceOverride);
            }

            if (enableChat)
            {
                var cfg = Configuration.Instance;
                var chatColor = UnturnedChat.GetColorFromName(cfg.ChatMessageColor, DefaultColor);
                Say(null, TextFormatter.Format(raw, null), chatColor);
            }
        }

        private string GetCurrentAnnouncementRaw()
        {
            var ann = Configuration.Instance.Announcements;
            if (ann == null || ann.Count == 0) return string.Empty;
            return _currentAnnouncementRaw ?? ann[0];
        }

        // ══════════════════════════════════════════════
        //  聊天消息
        // ══════════════════════════════════════════════

        public void Say(UnturnedPlayer player, string message, Color? color = null)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var cfg = Configuration.Instance;
            var chatColor = color ?? UnturnedChat.GetColorFromName(cfg.ChatMessageColor, DefaultColor);
            string iconUrl = cfg.ChatAvatarURL ?? "";

            if (!string.IsNullOrWhiteSpace(iconUrl))
            {
                string icon = iconUrl;
                TextFormatter.ReplaceVariables(ref icon, player);
                iconUrl = icon;
            }

            if (string.IsNullOrWhiteSpace(iconUrl))
            {
                if (player == null) UnturnedChat.Say(message, chatColor);
                else UnturnedChat.Say(player, message, chatColor);
                return;
            }

            try
            {
                if (player == null)
                {
                    var clients = Provider.clients;
                    for (int i = 0; i < clients.Count; i++)
                    {
                        ChatManager.serverSendMessage(
                            message, chatColor, null, clients[i],
                            EChatMode.GLOBAL, iconUrl, true);
                    }
                }
                else
                {
                    var steamPlayer = player.Player?.channel?.owner;
                    if (steamPlayer == null) return;

                    ChatManager.serverSendMessage(
                        message, chatColor, null, steamPlayer,
                        EChatMode.SAY, iconUrl, true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[AnnounceUI] Send message with avatar failed: {ex.Message}");
                if (player == null) UnturnedChat.Say(message, chatColor);
                else UnturnedChat.Say(player, message, chatColor);
            }
        }

        // ══════════════════════════════════════════════
        //  命令
        // ══════════════════════════════════════════════

        public void HandleAnnUICommand(UnturnedPlayer player, string[] args)
        {
            var state = GetOrCreateState(player, resetToDefault: false);
            var conn = GetConnection(player);

            if (args == null || args.Length == 0)
            {
                state.AllEnabled = !state.AllEnabled;
                if (conn != null) ApplyVisibility(player, conn, state);

                Say(player,
                    state.AllEnabled ? Translate("annui_all_on") : Translate("annui_all_off"),
                    YellowColor);
                return;
            }

            string a0 = args[0].ToLowerInvariant();

            if (a0 == "01" || a0 == "1")
            {
                if (!state.AllEnabled)
                {
                    Say(player, Translate("annui_all_off_hint"), YellowColor);
                    return;
                }
                state.RulesEnabled = !state.RulesEnabled;
                if (conn != null) ApplyVisibility(player, conn, state);

                Say(player,
                    state.RulesEnabled ? Translate("annui_rules_on") : Translate("annui_rules_off"),
                    YellowColor);
                return;
            }

            if (a0 == "02" || a0 == "2")
            {
                if (!state.AllEnabled)
                {
                    Say(player, Translate("annui_all_off_hint"), YellowColor);
                    return;
                }
                state.AnnouncementsEnabled = !state.AnnouncementsEnabled;
                if (conn != null) ApplyVisibility(player, conn, state);

                Say(player,
                    state.AnnouncementsEnabled ? Translate("annui_announce_on") : Translate("annui_announce_off"),
                    YellowColor);
                return;
            }

            Say(player, Translate("annui_usage"), YellowColor);
        }

        // ══════════════════════════════════════════════
        //  清理
        // ══════════════════════════════════════════════

        private void ClearUIForAllOnlinePlayers()
        {
            ushort effectId = Configuration.Instance.EffectId;

            foreach (Player p in PlayerTool.EnumeratePlayers())
            {
                try
                {
                    var conn = p.channel.owner.transportConnection;
                    EffectManager.askEffectClearByID(effectId, conn);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[AnnounceUI] ClearUI failed: {ex.Message}");
                }
            }
        }

        // ══════════════════════════════════════════════
        //  辅助方法
        // ══════════════════════════════════════════════

        private PlayerUIState GetOrCreateState(UnturnedPlayer player, bool resetToDefault)
        {
            ulong sid = player.CSteamID.m_SteamID;

            if (!_states.TryGetValue(sid, out var st))
            {
                st = new PlayerUIState();
                _states[sid] = st;
                resetToDefault = true;
            }

            if (resetToDefault)
            {
                st.EffectSent = false;
                st.AllEnabled = Configuration.Instance.ShowUIByDefault;
                st.RulesEnabled = true;
                st.AnnouncementsEnabled = true;
                st.IsHiddenByGameUI = false;
                st.IsInventoryOpen = false;
            }

            return st;
        }

        private static ITransportConnection GetConnection(UnturnedPlayer player)
        {
            return player.Player?.channel?.owner?.transportConnection;
        }

        private static ITransportConnection GetConnection(Player player)
        {
            return player?.channel?.owner?.transportConnection;
        }

        private bool IsOverrideActive()
        {
            return _overrideActive && DateTime.UtcNow < _overrideUntilUtc;
        }
    }
}
