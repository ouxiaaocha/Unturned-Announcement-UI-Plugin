using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;
using UnityEngine;

namespace AnnounceUI
{
    public class AnnCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "ann";
        public string Help => "强制广播一条公告（优先于轮播）";
        public string Syntax => "/ann <时间(秒)> <文字>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ann" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var player = caller as UnturnedPlayer;
            if (player == null) return;

            var plugin = AnnouncementUIPlugin.Instance;
            if (plugin == null)
            {
                UnturnedChat.Say(player, "插件未加载或实例为空，请重启服务器。", Color.red);
                return;
            }

            if (command == null || command.Length < 2)
            {
                UnturnedChat.Say(player, plugin.Translate("ann_usage"), Color.yellow);
                return;
            }

            string first = command[0];
            if (!int.TryParse(first, out int durationSeconds))
            {
                UnturnedChat.Say(player, plugin.Translate("ann_time_invalid"), Color.yellow);
                return;
            }

            string message = string.Join(" ", command, 1, command.Length - 1);
            if (string.IsNullOrWhiteSpace(message))
            {
                UnturnedChat.Say(player, plugin.Translate("ann_usage"), Color.yellow);
                return;
            }

            plugin.StartForcedAnnouncement(message, durationSeconds);

            UnturnedChat.Say(player, plugin.Translate("ann_sent", durationSeconds), Color.green);
        }
    }
}
