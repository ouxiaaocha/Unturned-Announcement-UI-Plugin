using Rocket.Core.Plugins;
using System;
using System.IO;
using System.Text;
using Logger = Rocket.Core.Logging.Logger;

namespace AnnounceUI
{
    public static class PluginDocsGenerator
    {
        public static void WriteDocs(RocketPlugin plugin)
        {
            try
            {
                string dir = plugin.Directory;
                string path = Path.Combine(dir, "AnnounceUI_Usage.txt");

                var sb = new StringBuilder();

                sb.AppendLine("========================================");
                sb.AppendLine("AnnounceUI 插件说明");
                sb.AppendLine("========================================");
                sb.AppendLine("插件制作人：欧小茶");
                sb.AppendLine("联系方式：QQ56896686");
                sb.AppendLine("插件权限组：ann");
                sb.AppendLine("");

                sb.AppendLine("【插件功能】");
                sb.AppendLine("1) 公告轮播：从配置文件 Announcements 列表按时间轮播显示在 UI 上。");
                sb.AppendLine("2) 规则 UI：从配置文件 Rules 列表映射到 ServerRulesText / ServerRulesText1 / 2 / 3 ...");
                sb.AppendLine("3) 可开关：/ANNUI 总开关，/ANNUI 01 规则 UI，/ANNUI 02 公告 UI。");
                sb.AppendLine("4) 管理强制广播：/ann <时间(秒)> <文字>（需要 ann 权限）。强制广播期间暂停轮播，到期恢复。");
                sb.AppendLine("");

                sb.AppendLine("【命令用法】");
                sb.AppendLine("/ANNUI");
                sb.AppendLine("  - 关闭/开启所有 UI（Canvas）");
                sb.AppendLine("/ANNUI 01");
                sb.AppendLine("  - 关闭/开启 服务器规则 UI（ServerRulesUI）");
                sb.AppendLine("/ANNUI 02");
                sb.AppendLine("  - 关闭/开启 服务器公告 UI（AnnouncementUI）");
                sb.AppendLine("/ann <时间(秒)> <文字>");
                sb.AppendLine("  - 强制广播公告（优先于轮播）。示例：/ann 60 {color=#ff0000}维护中{/color}");
                sb.AppendLine("");

                sb.AppendLine("【富文本格式（配置内使用 {}）】");
                sb.AppendLine("Bold  - {b}Bold{/b}");
                sb.AppendLine("Italic- {i}Italic{/i}");
                sb.AppendLine("Color - {color=#3498db}Color{/color}");
                sb.AppendLine("Size  - {size=20}Size{/size}");
                sb.AppendLine("换行  - {br}");
                sb.AppendLine("");
                sb.AppendLine("说明：插件会把 { } 自动转换为 < > 以支持 Unity/TMP 富文本。{br} 会转换成换行。");
                sb.AppendLine("");

                sb.AppendLine("【变量（可选）】");
                sb.AppendLine("{player_name}  玩家名称");
                sb.AppendLine("{player_id}    玩家ID");
                sb.AppendLine("{server_name}  服务器名");
                sb.AppendLine("{server_players} 当前人数");
                sb.AppendLine("{server_maxplayers} 最大人数");
                sb.AppendLine("{server_map}   地图名");
                sb.AppendLine("{server_mode}  模式");
                sb.AppendLine("");

                sb.AppendLine("【权限说明】");
                sb.AppendLine("权限：ann");
                sb.AppendLine("  - 允许使用 /ann 强制广播命令。");
                sb.AppendLine("");
                sb.AppendLine("========================================");

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[AnnounceUI] WriteDocs failed: {ex.Message}");
            }
        }
    }
}
