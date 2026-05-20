using Rocket.API;
using SDG.Unturned;

namespace AnnounceUI
{
    public static class TextFormatter
    {
        public static string Format(string raw, IRocketPlayer player)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            string text = raw;

            ReplaceVariables(ref text, player);

            text = text.Replace("{br}", "\n").Replace("{BR}", "\n");
            text = text.Replace("{", "<").Replace("}", ">");

            return text;
        }

        public static void ReplaceVariables(ref string text, IRocketPlayer player)
        {
            string playerName = player != null ? SanitizePlayerName(player.DisplayName) : string.Empty;
            string playerId = player != null ? player.Id : string.Empty;

            string serverName = Provider.serverName;
            string players = Provider.clients.Count.ToString();
            string maxPlayers = Provider.maxPlayers.ToString();
            string map = Level.info != null ? Level.info.name : string.Empty;
            string mode = Provider.mode.ToString();

            text = text
                .Replace("{player_name}", playerName)
                .Replace("{player_id}", playerId)
                .Replace("{server_name}", serverName)
                .Replace("{server_players}", players)
                .Replace("{server_maxplayers}", maxPlayers)
                .Replace("{server_map}", map)
                .Replace("{server_mode}", mode);
        }

        private static string SanitizePlayerName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.Replace("{", "").Replace("}", "");
        }
    }
}
