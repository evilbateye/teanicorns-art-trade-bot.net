using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Commands;
using Discord;

namespace teanicorns_art_trade_bot
{
    public class Utils
    {
        //public const string ADMIN_GROUP_ID = "admin";

        public static bool IsAdminUser(SocketUser user)
        {
            if (user is SocketGuildUser guildUser)
                return guildUser.GuildPermissions.Administrator;

            var guild = user.MutualGuilds.FirstOrDefault();
            if (guild == null)
                return false;

            var userList = guild.Users.ToList();
            int index = userList.FindIndex(x => x.Id == user.Id);
            if (index == -1)
                return false;

            return userList[index].GuildPermissions.Administrator;
        }

        public static bool IsAdminCommand(CommandInfo cmd)
        {
            //return cmd.Module.Group == ADMIN_GROUP_ID;
            return cmd.Module.Name == "TradeEventModule";
        }

        public static SocketUser FindUser(SocketGuild guild, string userId)
        {
            int index = -1;
            var userList = guild.Users.ToList();
            
            if (UInt64.TryParse(userId, out ulong numericId))
                index = userList.FindIndex(x => x.Id == numericId);
            else
                index = userList.FindIndex(x => x.Username == userId || x.Nickname == userId);

            return index == -1 ? null : userList[index];
        }

        public static SocketGuild FindGuild(SocketUser user)
        {
            if (user is SocketGuildUser guildUser)
                return guildUser.Guild;

            return user.MutualGuilds.FirstOrDefault();
        }

        public static SocketGuild FindGuild(DiscordSocketClient socketClient)
        {
            return socketClient.Guilds.FirstOrDefault();
        }

        public static SocketTextChannel FindChannel(DiscordSocketClient socketClient, string channel)
        {
            SocketGuild guild = FindGuild(socketClient);
            if (guild == null)
                return null;
            return FindChannel(guild, channel);
        }

        public static SocketTextChannel FindChannel(SocketGuild guild, string channel)
        {
            if (guild == null)
                return null;

            foreach (SocketTextChannel txtChannel in guild.TextChannels)
            {
                if (txtChannel.Name == channel)
                    return txtChannel;
            }

            return null;
        }

        public static async Task<string> GetThemePollResult(SocketTextChannel channel)
        {
            if (Storage.xs.Settings.GetThemePollID() == 0)
                return "";

            var msg = await channel.GetMessageAsync(Storage.xs.Settings.GetThemePollID());

            List<(string, int)> emojiCodeReactions = new List<(string, int)>();
            foreach (var emoji in msg.Reactions)
            {
                string emojiCode = emoji.Key.Name;
                if (EmojiCodes.Contains(emojiCode))
                    emojiCodeReactions.Add((emojiCode, emoji.Value.ReactionCount));
            }
            emojiCodeReactions = emojiCodeReactions.OrderByDescending(x => x.Item2).ToList();

            if (emojiCodeReactions.Count == 0)
                return "";

            List<string> winners = new List<string>();
            winners.Add(emojiCodeReactions[0].Item1);
            int maxReactions = emojiCodeReactions[0].Item2;

            for (int i = 1; i < emojiCodeReactions.Count; ++i)
            {
                if (emojiCodeReactions[i].Item2 < maxReactions)
                    break;
                winners.Add(emojiCodeReactions[i].Item1);
            }

            string winnerCode = winners[0];
            if (winners.Count > 1)
                winnerCode = winners.OrderBy(x => Guid.NewGuid()).First();

            int winnerIdx = EmojiCodes.IndexOf(winnerCode);
            if (winnerIdx < 0 || winnerIdx > 9)
                return "";

            var themePool = GetThemePoolOrdered();
            if (themePool.Count <= 0)
                return "";

            await channel.DeleteMessageAsync(msg);
            return themePool[winnerIdx];
        }

        public static List<string> GetThemePoolOrdered()
        {
            List<string> themePool = new List<string>();
            var pools2darr = Storage.xs.Entries.GetStorage().Select(x => new List<string>(x.ThemePool)).ToList();
            while (pools2darr.Count > 0)
            {
                for (int i = pools2darr.Count - 1; i >= 0; --i)
                {
                    if (pools2darr[i].Count <= 0)
                    {
                        pools2darr.RemoveAt(i);
                        continue;
                    }

                    themePool.Add(pools2darr[i][0]);
                    pools2darr[i].RemoveAt(0);
                }
            }

            if (themePool.Count > 10)
                themePool.RemoveRange(10, themePool.Count - 10);

            return themePool;
        }

        public static List<string> EmojiCodes = new List<string>() { "\u0030\u20E3" /*:zero:*/, "\u0031\u20E3" /*:one:*/, "\u0032\u20E3" /*:two:*/
            , "\u0033\u20E3" /*:three:*/, "\u0034\u20E3" /*:four:*/, "\u0035\u20E3" /*:five:*/, "\u0036\u20E3" /*:six:*/, "\u0037\u20E3" /*:seven:*/
            , "\u0038\u20E3" /*:eight:*/, "\u0039\u20E3" /*:nine:*/};

        public static async Task NotifySubscribers(DiscordSocketClient client, string message, List<ulong> subscribers = null)
        {
            if (subscribers == null)
                subscribers = Storage.xs.Settings.GetSubscribers();

            foreach (ulong userId in subscribers)
            {
                SocketUser su = client.GetUser(userId);
                if (su != null)
                    await su.SendMessageAsync($"<@{userId}> " + message);
            }
        }
    }
}
