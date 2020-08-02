using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Commands;
using Discord;
using teanicorns_art_trade_bot.Storage;

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

            var channel = FindChannel(client, xs.Settings.GetWorkingChannel());
            if (channel == null)
                return null;

            return await channel.GetMessageAsync(messageID);
        }

        public static async Task<string> GetThemePollResult(DiscordSocketClient client)
        {
            var msg = await FindChannelMessage(client, xs.Settings.GetThemePollMessageId());
            if (msg == null)
                return "";

            var channel = FindChannel(client, xs.Settings.GetWorkingChannel());
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

            await EditMessagePin(client, msg.Id, false /*unpin*/);
            //await channel.DeleteMessageAsync(msg);
            xs.Settings.SetThemePollID(0);

            var winner = themePool[winnerIdx];
            xs.Settings.RemoveThemeFromPool(winner.Item1, winner.Item2);
            return winner.Item2;
        }

        public static List<(ulong, string)> GetThemePoolOrdered()
        {
            List<(ulong, string)> themePool = new List<(ulong, string)>();
            List<(ulong, List<string>)> pools2darr = xs.Settings.GetThemePool().Select(pair => (pair.Key, new List<string>(pair.Value))).ToList();
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

            int maxThemes = ApplicationSettings.MAX_THEMES_COUNT;
            if (themePool.Count > maxThemes)
                themePool.RemoveRange(maxThemes, themePool.Count - maxThemes);

            return themePool;
        }

        public static List<string> EmojiCodes = new List<string>() { "\uD83C\uDF51" /*:peach:*/, "\uD83C\uDF52" /*:cherries:*/, "\uD83C\uDF53" /*:strawberry:*/
            , "\uD83C\uDF47" /*:grapes:*/, "\uD83C\uDF4E" /*:apple:*/, "\uD83C\uDF50" /*:pear:*/, "\uD83C\uDF49" /*:watermelon:*/, "\uD83C\uDF4A" /*:tangerine:*/
            , "\uD83C\uDF4C" /*:banana:*/, "\uD83C\uDF4D" /*:pineapple:*/};

        public static async Task<bool> CreateThemePoll(DiscordSocketClient client)
        {
            if (xs.Settings.GetThemePollMessageId() != 0)
                return false;

            SocketTextChannel channel = FindChannel(client, xs.Settings.GetWorkingChannel());
            if (channel == null)
                return false;

            var reply = $"{string.Format(Properties.Resources.TRADE_THEME_POOL_START)}";
            List<Emoji> emojiObjs = new List<Emoji>();
            List<string> themePool = GetThemePoolOrdered().Select(x => x.Item2).ToList();
            if (themePool.Count > 0)
            {
                reply += $"\n({string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, "add theme <theme name>", "add a theme into the poll")})";
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

                    string emojiCode = EmojiCodes[i];
                    emojiObjs.Add(new Emoji(emojiCode));
                    reply += $"\n{emojiCode} : `{themePool[i]}`";
                }
            }
            else
            {
                reply += $"\n({string.Format(Properties.Resources.GLOBAL_EMPTY, "themes")}, {string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, "add theme <theme name>", "add a theme into the poll")})";
            }

            var msg = await channel.SendMessageAsync(reply);
            xs.Settings.SetThemePollID(msg.Id);
            emojiObjs.ForEach(async e => await msg.AddReactionAsync(e));
            await EditMessagePin(client, msg.Id, true /*pin*/);

            await NotifySubscribers(client, string.Format(Properties.Resources.TRADE_THEME_POOL_SUBS, "theme poll", xs.Settings.GetWorkingChannel()));
            return true;
        }

        public static async Task<bool> EditThemePoll(DiscordSocketClient client)
        {
            if (xs.Settings.GetThemePollMessageId() == 0)
                return false;

            var msg = await FindChannelMessage(client, xs.Settings.GetThemePollMessageId());
            if (msg == null)
                return false;

            var restMsg = msg as Discord.Rest.RestUserMessage;
            if (restMsg == null)
                return false;

            List<Emoji> emojiObjs = new List<Emoji>();
            var reply = $"{string.Format(Properties.Resources.TRADE_THEME_POOL_START)}";
            List<string> themePool = GetThemePoolOrdered().Select(x => x.Item2).ToList();
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
                reply += $"\n{string.Format(Properties.Resources.GLOBAL_EMPTY, Config.CmdPrefix, "add theme <theme name>")}";
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
                subscribers = xs.Settings.GetSubscribers();

            foreach (ulong userId in subscribers)
            {
                SocketUser su = client.GetUser(userId);
                if (su != null)
                    await su.SendMessageAsync($"<@{userId}> " + message, embed: Utils.EmbedFooter(client));
            }
        }

        public static EmbedBuilder GetFooterBuilder(DiscordSocketClient client)
        {
            SocketTextChannel channel = FindChannel(client, xs.Settings.GetWorkingChannel());
            if (channel == null)
                return null;

            List<string> footer = new List<string>();

            if (xs.Settings.GetThemePollMessageId() != 0)
                footer.Add($"[theme poll](https://discordapp.com/channels/{FindGuild(client).Id}/{channel.Id}/{xs.Settings.GetThemePollMessageId()})");

            if (xs.Settings.GetHelpMessageId() != 0)
                footer.Add($"[bot help](https://discordapp.com/channels/{FindGuild(client).Id}/{channel.Id}/{xs.Settings.GetHelpMessageId()})");

            footer.Add($"[teanicorn web](https://teanicorns.weebly.com/)");

            string avatarUrl = client.CurrentUser.GetAvatarUrl();
            if (string.IsNullOrEmpty(avatarUrl))
                avatarUrl = client.CurrentUser.GetDefaultAvatarUrl();

            return new EmbedBuilder()
                .WithColor(51, 144, 243)
                .WithDescription(string.Join(" **|** ", footer));
        }

        public static Embed EmbedFooter(DiscordSocketClient client)
        {
            return GetFooterBuilder(client).Build();
        }

        public enum AboutMessageSubtype
        {
            intro = 0,
            userCommands = 1,
            adminCommands = 2
        }
        public static List<string> CreateAbout(CommandService commandService, bool adminUser)
        {
            var ret = new List<string>();
            ret.Add($"{string.Format(Properties.Resources.INFO_INTRO, DiscordConfig.Version, Config.CmdPrefix, "about", "set entry")}\n");
            string about = $"**User Commands**\n";
            string adminAbout = adminUser ? $"**Admin commands**\n" : "";

            foreach (var cmd in commandService.Commands)
            {
                var par = cmd.Parameters;

                string aliases = $"`{Config.CmdPrefix}{cmd.Aliases.FirstOrDefault()}`";
                if (!string.IsNullOrWhiteSpace(aliases))
                {
                    for (int i = 1; i < cmd.Aliases.Count; ++i)
                        aliases += $" | `{Config.CmdPrefix}{cmd.Aliases.ElementAt(i)}`";
                }

                if (IsAdminCommand(cmd))
                {
                    if (adminUser)
                        adminAbout += $"{aliases} : {cmd.Summary}\n";
                }
                else
                    about += $"{aliases} : {cmd.Summary}\n";
            }

            ret.Add(about);
            ret.Add(adminAbout);
            return ret;
        }

        public static async Task EditMessagePin(DiscordSocketClient client, ulong msgId, bool bPin)
        {
            var oldHelpMsg = await FindChannelMessage(client, msgId);
            if (oldHelpMsg != null && oldHelpMsg is Discord.Rest.RestUserMessage)
            {
                var oldRestHelpMsg = (Discord.Rest.RestUserMessage)oldHelpMsg;
                if (bPin)
                    await oldRestHelpMsg.PinAsync();
                else
                    await oldRestHelpMsg.UnpinAsync();
            }
        }

        public static async Task<bool> CreateHelp(DiscordSocketClient client, CommandService commandService, string channelName)
        {
            channelName = channelName.Trim();
            if (string.IsNullOrWhiteSpace(channelName))
                channelName = xs.Settings.GetWorkingChannel();

            SocketTextChannel channel = FindChannel(client, channelName);
            if (channel == null)
                return false;

            await EditMessagePin(client, xs.Settings.GetHelpMessageId(), false /*unpin*/);

            var aboutMsgs = CreateAbout(commandService, false);
            var helpMsg = await channel.SendMessageAsync($"@everyone {aboutMsgs[(int)AboutMessageSubtype.intro]}\n{aboutMsgs[(int)AboutMessageSubtype.userCommands]}");
            xs.Settings.SetHelpMessageId(helpMsg.Id);

            await EditMessagePin(client, helpMsg.Id, true /*pin*/);
            return true;
        }
    }
}
