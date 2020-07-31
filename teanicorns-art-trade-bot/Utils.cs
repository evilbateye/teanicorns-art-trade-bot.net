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

        public static async Task<IMessage> FindChannelMessage(DiscordSocketClient client, ulong messageID)
        {
            if (messageID == 0)
                return null;

            var channel = Utils.FindChannel(client, Storage.xs.Settings.GetWorkingChannel());
            if (channel == null)
                return null;

            return await channel.GetMessageAsync(messageID);
        }

        public static async Task<string> GetThemePollResult(DiscordSocketClient client)
        {
            var msg = await FindChannelMessage(client, Storage.xs.Settings.GetThemePollID());
            if (msg == null)
                return "";

            var channel = Utils.FindChannel(client, Storage.xs.Settings.GetWorkingChannel());
            if (channel == null)
                return "";

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
            Storage.xs.Settings.SetThemePollID(0);

            var winner = themePool[winnerIdx];
            Storage.xs.Settings.RemoveThemeFromPool(winner.Item1, winner.Item2);
            return winner.Item2;
        }

        public static List<(ulong, string)> GetThemePoolOrdered()
        {
            List<(ulong, string)> themePool = new List<(ulong, string)>();
            List<(ulong, List<string>)> pools2darr = Storage.xs.Settings.GetThemePool().Select(pair => (pair.Key, new List<string>(pair.Value))).ToList();
            while (pools2darr.Count > 0)
            {
                for (int i = pools2darr.Count - 1; i >= 0; --i)
                {
                    if (pools2darr[i].Item2.Count <= 0)
                    {
                        pools2darr.RemoveAt(i);
                        continue;
                    }

                    themePool.Add((pools2darr[i].Item1, pools2darr[i].Item2[0]));
                    pools2darr[i].Item2.RemoveAt(0);
                }
            }

            int maxThemes = Storage.ApplicationSettings.MAX_THEMES_COUNT;
            if (themePool.Count > maxThemes)
                themePool.RemoveRange(maxThemes, themePool.Count - maxThemes);

            return themePool;
        }

        public static List<string> EmojiCodes = new List<string>() { "\uD83C\uDF51" /*:peach:*/, "\uD83C\uDF52" /*:cherries:*/, "\uD83C\uDF53" /*:strawberry:*/
            , "\uD83C\uDF47" /*:grapes:*/, "\uD83C\uDF4E" /*:apple:*/, "\uD83C\uDF50" /*:pear:*/, "\uD83C\uDF49" /*:watermelon:*/, "\uD83C\uDF4A" /*:tangerine:*/
            , "\uD83C\uDF4C" /*:banana:*/, "\uD83C\uDF4D" /*:pineapple:*/};

        public static async Task<bool> CreateThemePoll(DiscordSocketClient client)
        {
            if (Storage.xs.Settings.GetThemePollID() != 0)
                return false;

            SocketTextChannel channel = Utils.FindChannel(client, Storage.xs.Settings.GetWorkingChannel());
            if (channel == null)
                return false;

            var reply = $"{string.Format(Properties.Resources.TRADE_THEME_POOL_START)}";
            List<Emoji> emojiObjs = new List<Emoji>();
            List<string> themePool = Utils.GetThemePoolOrdered().Select(x => x.Item2).ToList();
            if (themePool.Count > 0)
            {
                //Encoding unicode = Encoding.Unicode;
                //byte[] bytes = new byte[] { 48, 0, 227, 32 }; // ::zero::
                
                for (int i = 0; i < themePool.Count; ++i)
                {
                    //var bytes = BitConverter.GetBytes(emojiNumber);
                    //var emojistr2 = "\u0030\u20E3";
                    //var bytes2 = unicode.GetBytes(emojistr2);
                    //var bytes2int32u = BitConverter.ToUInt32(bytes2, 0);

                    //var emojiCode = unicode.GetString(bytes);
                    //bytes[0] += 1;

                    string emojiCode = Utils.EmojiCodes[i];
                    emojiObjs.Add(new Emoji(emojiCode));
                    reply += $"\n{emojiCode} : `{themePool[i]}`";
                }
            }
            else
            {
                reply += $"\n{string.Format(Properties.Resources.TRADE_THEME_POOL_EMPTY, Config.CmdPrefix, "add theme <theme name>")}";
            }

            var msg = await channel.SendMessageAsync(reply);
            Storage.xs.Settings.SetThemePollID(msg.Id);
            emojiObjs.ForEach(async e => await msg.AddReactionAsync(e));

            await Utils.NotifySubscribers(client, string.Format(Properties.Resources.TRADE_THEME_POOL_SUBS, "theme poll", Storage.xs.Settings.GetWorkingChannel()));
            return true;
        }

        public static async Task<bool> EditThemePoll(DiscordSocketClient client)
        {
            if (Storage.xs.Settings.GetThemePollID() == 0)
                return false;

            var msg = await Utils.FindChannelMessage(client, Storage.xs.Settings.GetThemePollID());
            if (msg == null)
                return false;

            var restMsg = msg as Discord.Rest.RestUserMessage;
            if (restMsg == null)
                return false;

            List<Emoji> emojiObjs = new List<Emoji>();
            var reply = $"{string.Format(Properties.Resources.TRADE_THEME_POOL_START)}";
            List<string> themePool = Utils.GetThemePoolOrdered().Select(x => x.Item2).ToList();
            if (themePool.Count > 0)
            {
                List<string> emojiCodesTmp = new List<string>(EmojiCodes);
                string content = restMsg.Content;
                string[] lines = content.Split('\n');
                foreach (string line in lines)
                {
                    string[] themeLine = line.Split(':');
                    if (themeLine.Count() != 2)
                        continue;
                    string contentEmojiCode = themeLine[0].ToLower().Trim();
                    if (!EmojiCodes.Contains(contentEmojiCode))
                        continue;
                    string contentTheme = themeLine[1].Replace('`', ' ').ToLower().Trim();
                    if (!themePool.Contains(contentTheme))
                        continue;
                    reply += $"\n{line}";
                    emojiObjs.Add(new Emoji(contentEmojiCode));
                    emojiCodesTmp.Remove(contentEmojiCode);
                    themePool.RemoveAll(x => x == contentTheme);
                }

                foreach (string artTheme in themePool)
                {
                    string tmpEmojiCode = emojiCodesTmp[0];
                    reply += $"\n{tmpEmojiCode} : `{artTheme}`";
                    emojiObjs.Add(new Emoji(tmpEmojiCode));
                    emojiCodesTmp.RemoveAt(0);
                }
            }
            else
            {
                reply += $"\n{string.Format(Properties.Resources.TRADE_THEME_POOL_EMPTY, Config.CmdPrefix, "add theme <theme name>")}";
            }

            await restMsg.ModifyAsync(x => x.Content = reply);
            foreach (var emoji in restMsg.Reactions)
            {
                string emojiCode = emoji.Key.Name;
                var foundEmoji = emojiObjs.FirstOrDefault(x => x.Name == emojiCode);
                if (foundEmoji == null)
                {
                    var usersReacted = restMsg.GetReactionUsersAsync(emoji.Key, 100);
                    await usersReacted.ForEachAsync(x => x.ToList().ForEach(u => restMsg.RemoveReactionAsync(emoji.Key, u)));
                }
                else
                {
                    emojiObjs.Remove(foundEmoji);
                }
            }

            foreach (var emojiObj in emojiObjs)
            {
                var foundReact = restMsg.Reactions.FirstOrDefault(x => x.Key.Name == emojiObj.Name).Key;
                if (foundReact == null)
                    await restMsg.AddReactionAsync(emojiObj);
            }
            return true;
        }

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
