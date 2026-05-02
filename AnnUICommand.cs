using Rocket.API;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace AnnounceUI
{
    public class AnnUICommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "annui";
        public string Help => "关闭/开启公告与规则UI";
        public string Syntax => "/annui | /annui 01 | /annui 02";

        public List<string> Aliases => new List<string> { "ANNUI" };
        public List<string> Permissions => new List<string>();

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var player = caller as UnturnedPlayer;
            if (player == null) return;

            AnnouncementUIPlugin.Instance?.HandleAnnUICommand(player, command);
        }
    }
}
