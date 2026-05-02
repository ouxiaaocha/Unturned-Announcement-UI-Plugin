using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Core.Utils;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.NetTransport;
using SDG.Unturned;
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
        private int _announceIndex = 0;
        private string _currentAnnouncementRaw = null;

        private bool _overrideActive = false;
        private DateTime _overrideUntilUtc;
        private string _overrideMessageRaw;

        public override TranslationList DefaultTranslations => new TranslationList()
        {
            { "annui_all_on", "AnnUI: 已开启全部UI" },
            { "annui_all_off", "AnnUI: 已关闭全部UI" },
            { "annui_rules_on", "AnnUI: 已开启 服务器规则UI (01)" },
            { "annui_rules_off", "AnnUI: 已关闭 服务器规则UI (01)" },
            { "annui_announce_on", "AnnUI: 已开启 服务器公告UI (02)" },
            { "annui_announce_off", "AnnUI: 已关闭 服务器公告UI (02)" },
            { "annui_usage", "用法: /ANNUI | /ANNUI 01 | /ANNUI 02" },

            { "ann_usage", "用法: /ann <文字> <时间(秒)>" },
            { "ann_time_invalid", "时间必须是数字(秒)。示例: /ann 维护中 60" },
            { "ann_sent", "已强制广播 {0} 秒。" },

            { "no_plugin_instance", "插件未加载或实例为空，请重启服务器。" },
        };

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

            StartAnnouncerTimer();
            PluginDocsGenerator.WriteDocs(this);

            Logger.Log($"[AnnounceUI] EffectId={Configuration.Instance.EffectId}, Key={EFFECT_KEY}", ConsoleColor.Yellow);
            Logger.Log($"[AnnounceUI] ChatAvatarURL: {Configuration.Instance.ChatAvatarURL}", ConsoleColor.Yellow);
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= OnPlayerConnected;
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;

            StopAnnouncerTimer();
            ClearUIForAllOnlinePlayers();

            _states.Clear();
            Instance = null;

            Logger.Log("[AnnounceUI] Unloaded.", ConsoleColor.Yellow);
        }

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            var state = GetOrCreateState(player, resetToDefault: true);

            EnsureUIEffectSent(player, state);

            UpdateStaticTexts(player);

            if (_overrideActive && DateTime.UtcNow < _overrideUntilUtc)
                UpdateAnnouncementTextForPlayer(player, forceOverride: true);
            else
                UpdateAnnouncementTextForPlayer(player, forceOverride: false);

            ApplyVisibility(player, state);
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            _states.Remove(player.CSteamID.m_SteamID);
        }

        public void StartForcedAnnouncement(string rawMessage, int durationSeconds)
        {
            if (durationSeconds <= 0) durationSeconds = 60;

            _overrideActive = true;
            _overrideMessageRaw = rawMessage ?? string.Empty;
            _overrideUntilUtc = DateTime.UtcNow.AddSeconds(durationSeconds);

            BroadcastAnnouncementNow(forceOverride: true);

            var t = new Timer(Math.Max(1, durationSeconds) * 1000d);
            t.AutoReset = false;
            t.Elapsed += (s, e) =>
            {
                TaskDispatcher.QueueOnMainThread(() =>
                {
                    _overrideActive = false;
                    _overrideMessageRaw = null;
                    BroadcastAnnouncementNow(forceOverride: false);
                    t.Dispose();
                });
            };
            t.Start();
        }

        private void StartAnnouncerTimer()
        {
            StopAnnouncerTimer();

            if (Configuration.Instance.AnnouncementIntervalSeconds <= 0) return;

            _timer = new Timer(Configuration.Instance.AnnouncementIntervalSeconds * 1000);
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

                BroadcastAnnouncementNow(forceOverride: _overrideActive);
            });
        }

        private void BroadcastAnnouncementNow(bool forceOverride)
        {
            string raw = forceOverride ? _overrideMessageRaw : GetCurrentAnnouncementRawFallback();
            if (string.IsNullOrEmpty(raw)) return;

            string formatted = TextFormatter.Format(raw, null);

            foreach (SteamPlayer sp in Provider.clients)
            {
                UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(sp);
                if (player == null) continue;

                UpdateAnnouncementTextForPlayer(player, forceOverride);
            }

            if (Configuration.Instance.EnableChatAnnouncements)
            {
                Color color = UnturnedChat.GetColorFromName(Configuration.Instance.ChatMessageColor, Color.white);
                Say(null, formatted, color);
            }
        }

        private string GetCurrentAnnouncementRawFallback()
        {
            var ann = Configuration.Instance.Announcements;
            if (ann == null || ann.Count == 0) return string.Empty;
            return _currentAnnouncementRaw ?? ann[0];
        }

        public void Say(UnturnedPlayer player, string message, Color? color = null)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var cfg = Configuration.Instance;
            var chatColor = color ?? UnturnedChat.GetColorFromName(cfg.ChatMessageColor, Color.white);
            string iconUrl = cfg.ChatAvatarURL ?? "";

            if (!string.IsNullOrWhiteSpace(iconUrl))
            {
                string icon = iconUrl;
                TextFormatter.ReplaceVariables(ref icon, player);
                iconUrl = icon;
            }

            if (string.IsNullOrWhiteSpace(iconUrl))
            {
                if (player == null)
                {
                    UnturnedChat.Say(message, chatColor);
                }
                else
                {
                    UnturnedChat.Say(player, message, chatColor);
                }
                return;
            }

            try
            {
                if (player == null)
                {
                    foreach (SteamPlayer sp in Provider.clients)
                    {
                        ChatManager.serverSendMessage(
                            message,
                            chatColor,
                            null,
                            sp,
                            EChatMode.GLOBAL,
                            iconUrl,
                            true
                        );
                    }
                }
                else
                {
                    SteamPlayer steamPlayer = player.Player?.channel?.owner;
                    if (steamPlayer == null) return;

                    ChatManager.serverSendMessage(
                        message,
                        chatColor,
                        null,
                        steamPlayer,
                        EChatMode.SAY,
                        iconUrl,
                        true
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[AnnounceUI] Send message with avatar failed: {ex.Message}");
                if (player == null)
                    UnturnedChat.Say(message, chatColor);
                else
                    UnturnedChat.Say(player, message, chatColor);
            }
        }

        private void UpdateStaticTexts(UnturnedPlayer player)
        {
            if (!Configuration.Instance.EnableRulesUI) return;

            var state = GetOrCreateState(player, resetToDefault: false);
            if (!state.AllEnabled || !state.RulesEnabled) return;

            EnsureUIEffectSent(player, state);
            var conn = player.Player.channel.owner.transportConnection;

            string title = TextFormatter.Format(Configuration.Instance.ServerTitleText ?? "服务器规则", player);
            EffectManager.sendUIEffectText(EFFECT_KEY, conn, true, "ServerText", title);

            var rules = Configuration.Instance.Rules ?? new List<string>();
            for (int i = 1; i <= Configuration.Instance.RulesFieldCount; i++)
            {
                string elementName = (i == 1) ? "ServerRulesText" : ("ServerRulesText" + (i - 1));
                string raw = (i <= rules.Count) ? rules[i - 1] : string.Empty;

                EffectManager.sendUIEffectText(EFFECT_KEY, conn, true, elementName, TextFormatter.Format(raw, player));
            }

            string help = TextFormatter.Format(Configuration.Instance.HelpText ?? string.Empty, player);
            EffectManager.sendUIEffectText(EFFECT_KEY, conn, true, "HelpText", help);
        }

        private void UpdateAnnouncementTextForPlayer(UnturnedPlayer player, bool forceOverride)
        {
            if (!Configuration.Instance.EnableUIAnnouncements) return;

            var state = GetOrCreateState(player, resetToDefault: false);
            if (!state.AllEnabled || !state.AnnouncementsEnabled) return;

            string raw = forceOverride ? (_overrideMessageRaw ?? string.Empty) : GetCurrentAnnouncementRawFallback();
            string formatted = TextFormatter.Format(raw, player);

            EnsureUIEffectSent(player, state);
            var conn = player.Player.channel.owner.transportConnection;
            EffectManager.sendUIEffectText(EFFECT_KEY, conn, true, "AnnouncementText", formatted);
        }

        private void ApplyVisibility(UnturnedPlayer player, PlayerUIState state)
        {
            EnsureUIEffectSent(player, state);

            var conn = player.Player.channel.owner.transportConnection;

            bool showRules = state.AllEnabled && state.RulesEnabled && Configuration.Instance.EnableRulesUI;
            bool showAnn = state.AllEnabled && state.AnnouncementsEnabled && Configuration.Instance.EnableUIAnnouncements;
            bool showCanvas = state.AllEnabled && (showRules || showAnn);

            EffectManager.sendUIEffectVisibility(EFFECT_KEY, conn, true, "Canvas", showCanvas);
            EffectManager.sendUIEffectVisibility(EFFECT_KEY, conn, true, "ServerRulesUI", showRules);
            EffectManager.sendUIEffectVisibility(EFFECT_KEY, conn, true, "AnnouncementUI", showAnn);

            if (showAnn)
            {
                bool inOverride = _overrideActive && DateTime.UtcNow < _overrideUntilUtc;
                UpdateAnnouncementTextForPlayer(player, forceOverride: inOverride);
            }
        }

        private void EnsureUIEffectSent(UnturnedPlayer player, PlayerUIState state)
        {
            if (state.EffectSent) return;

            var conn = player.Player.channel.owner.transportConnection;
            EffectManager.sendUIEffect(Configuration.Instance.EffectId, EFFECT_KEY, conn, true);

            state.EffectSent = true;
        }

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
                catch { }
            }
        }

        public void HandleAnnUICommand(UnturnedPlayer player, string[] args)
        {
            var state = GetOrCreateState(player, resetToDefault: false);

            if (args == null || args.Length == 0)
            {
                state.AllEnabled = !state.AllEnabled;
                ApplyVisibility(player, state);

                Say(player,
                    state.AllEnabled ? Translate("annui_all_on") : Translate("annui_all_off"),
                    Color.yellow);
                return;
            }

            string a0 = args[0].ToLowerInvariant();

            if (a0 == "01" || a0 == "1")
            {
                state.AllEnabled = true;
                state.RulesEnabled = !state.RulesEnabled;
                ApplyVisibility(player, state);

                Say(player,
                    state.RulesEnabled ? Translate("annui_rules_on") : Translate("annui_rules_off"),
                    Color.yellow);
                return;
            }

            if (a0 == "02" || a0 == "2")
            {
                state.AllEnabled = true;
                state.AnnouncementsEnabled = !state.AnnouncementsEnabled;
                ApplyVisibility(player, state);

                Say(player,
                    state.AnnouncementsEnabled ? Translate("annui_announce_on") : Translate("annui_announce_off"),
                    Color.yellow);
                return;
            }

            Say(player, Translate("annui_usage"), Color.yellow);
        }

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
            }

            return st;
        }
    }
}
