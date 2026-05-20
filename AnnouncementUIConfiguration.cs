using Rocket.API;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace AnnounceUI
{
    public class AnnouncementUIConfiguration : IRocketPluginConfiguration
    {
        public ushort EffectId { get; set; }

        public bool ShowUIByDefault { get; set; }

        public bool EnableUIAnnouncements { get; set; }
        public bool EnableChatAnnouncements { get; set; }
        public string ChatMessageColor { get; set; }

        public string ChatAvatarURL { get; set; }

        public double AnnouncementIntervalSeconds { get; set; }

        public bool EnableRulesUI { get; set; }
        public string ServerTitleText { get; set; }

        public int RulesFieldCount { get; set; }

        [XmlArrayItem("Rule")]
        public List<string> Rules { get; set; }

        public string HelpText { get; set; }

        [XmlArrayItem("Announcement")]
        public List<string> Announcements { get; set; }

        public void LoadDefaults()
        {
            EffectId = 45685;

            ShowUIByDefault = true;

            EnableUIAnnouncements = true;
            EnableChatAnnouncements = false;
            ChatMessageColor = "white";

            ChatAvatarURL = "https://s41.ax1x.com/2026/02/10/pZHE6fg.png";

            AnnouncementIntervalSeconds = 60;

            EnableRulesUI = true;
            ServerTitleText = "服务器规则";

            RulesFieldCount = 4;
            Rules = new List<string>
            {
                "这是你的第1条规则",
                "这是你的第2条规则",
                "这是你的第3条规则",
                "这是你的第4条规则"
            };

            HelpText = "使用命令 /ANNUI <编号> 关闭不同的UI";

            Announcements = new List<string>
            {
                "{b}欢迎来到服务器{/b}！",
                "公告示例：{color=#3498db}蓝色文字{/color} + {size=20}大号字体{/size}",
                "支持换行：第一行{br}第二行",
                "使用 /ANNUI 01 关闭规则UI，/ANNUI 02 关闭公告UI",
                "公告条目支持继续扩展"
            };
        }
    }

    internal class PlayerUIState
    {
        public bool EffectSent { get; set; }
        public bool AllEnabled { get; set; } = true;
        public bool RulesEnabled { get; set; } = true;
        public bool AnnouncementsEnabled { get; set; } = true;
    }
}
