﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Commands;
using Discord;
using teanicorns_art_trade_bot.Storage;
using System.Text.RegularExpressions;

namespace teanicorns_art_trade_bot
{
    public class Utils
    {
        //public const string ADMIN_GROUP_ID = "admin";
        private static Random rng = new Random();
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

            ulong numericId = 0;
            if (MentionUtils.TryParseUser(userId, out numericId))
                index = userList.FindIndex(x => x.Id == numericId);
            else if (UInt64.TryParse(userId, out numericId))
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
                if (EmojiCodes.Contains(emojiCode) || Utils.EmojiPattern.Match(emojiCode).Success)
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

            var themePool = GetThemePoolOrdered();
            if (themePool.Count <= 0)
                return "";

            var winner = themePool.FirstOrDefault(x => x.Item2.EmojiCode == winnerCode);
            if (winner == default)
                return "";

            await EditMessagePin(client, msg.Id, false /*unpin*/);
            //await channel.DeleteMessageAsync(msg);
            xs.Settings.SetThemePollID(0);
            xs.Settings.RemoveThemeFromPool(winner.Item1, winner.Item2.Theme);
            return winner.Item2.Theme;
        }

        public static List<(ulong, ArtTheme)> GetThemePoolOrdered()
        {
            List<(ulong, ArtTheme)> themePool = new List<(ulong, ArtTheme)>();
            List<(ulong, List<ArtTheme>)> pools2darr = xs.Settings.GetThemePool().Select(pair => (pair.Key, new List<ArtTheme>(pair.Value))).ToList();
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

        public static Regex EmojiPattern = new Regex(@"(?:\uD83D(?:\uDD73\uFE0F?|\uDC41(?:(?:\uFE0F(?:\u200D\uD83D\uDDE8\uFE0F?)?|\u200D\uD83D\uDDE8\uFE0F?))?|[\uDDE8\uDDEF]\uFE0F?|\uDC4B(?:\uD83C[\uDFFB-\uDFFF])?|\uDD90(?:(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F))?|[\uDD96\uDC4C\uDC48\uDC49\uDC46\uDD95\uDC47\uDC4D\uDC4E\uDC4A\uDC4F\uDE4C\uDC50\uDE4F\uDC85\uDCAA\uDC42\uDC43\uDC76\uDC66\uDC67](?:\uD83C[\uDFFB-\uDFFF])?|\uDC71(?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2640\u2642]\uFE0F?))?)|\u200D(?:[\u2640\u2642]\uFE0F?)))?|\uDC68(?:(?:\uD83C(?:\uDFFB(?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFC-\uDFFF]|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?|\uDFFC(?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF]|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?|\uDFFD(?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?|\uDFFE(?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF]|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?|\uDFFF(?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFE]|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?)|\u200D(?:\uD83E[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD]|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D(?:\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|\uDC68\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92])|\u2708\uFE0F?|\u2764(?:\uFE0F\u200D\uD83D(?:\uDC8B\u200D\uD83D\uDC68|\uDC68)|\u200D\uD83D(?:\uDC8B\u200D\uD83D\uDC68|\uDC68)))))?|\uDC69(?:(?:\uD83C(?:\uDFFB(?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC69\uD83C[\uDFFC-\uDFFF]|\uDC68\uD83C[\uDFFC-\uDFFF])|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?|\uDFFC(?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC69\uD83C[\uDFFB\uDFFD-\uDFFF]|\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF])|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?|\uDFFD(?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC69\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF])|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?|\uDFFE(?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC69\uD83C[\uDFFB-\uDFFD\uDFFF]|\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF])|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?|\uDFFF(?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC69\uD83C[\uDFFB-\uDFFE]|\uDC68\uD83C[\uDFFB-\uDFFE])|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?)|\u200D(?:\uD83E[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD]|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D(?:\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92])|\u2708\uFE0F?|\u2764(?:\uFE0F\u200D\uD83D(?:\uDC8B\u200D\uD83D[\uDC68\uDC69]|[\uDC68\uDC69])|\u200D\uD83D(?:\uDC8B\u200D\uD83D[\uDC68\uDC69]|[\uDC68\uDC69])))))?|[\uDC74\uDC75](?:\uD83C[\uDFFB-\uDFFF])?|[\uDE4D\uDE4E\uDE45\uDE46\uDC81\uDE4B\uDE47\uDC6E](?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|\uDD75(?:(?:\uFE0F(?:\u200D(?:[\u2642\u2640]\uFE0F?))?|\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\uDC82\uDC77](?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|\uDC78(?:\uD83C[\uDFFB-\uDFFF])?|\uDC73(?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\uDC72\uDC70\uDC7C](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC86\uDC87\uDEB6](?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\uDC83\uDD7A](?:\uD83C[\uDFFB-\uDFFF])?|\uDD74(?:(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F))?|\uDC6F(?:\u200D(?:[\u2642\u2640]\uFE0F?))?|[\uDEA3\uDEB4\uDEB5](?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\uDEC0\uDECC\uDC6D\uDC6B\uDC6C](?:\uD83C[\uDFFB-\uDFFF])?|\uDDE3\uFE0F?|\uDC15(?:\u200D\uD83E\uDDBA)?|[\uDC3F\uDD4A\uDD77\uDD78\uDDFA\uDEE3\uDEE4\uDEE2\uDEF3\uDEE5\uDEE9\uDEF0\uDECE\uDD70\uDD79\uDDBC\uDD76\uDECD\uDDA5\uDDA8\uDDB1\uDDB2\uDCFD\uDD6F\uDDDE\uDDF3\uDD8B\uDD8A\uDD8C\uDD8D\uDDC2\uDDD2\uDDD3\uDD87\uDDC3\uDDC4\uDDD1\uDDDD\uDEE0\uDDE1\uDEE1\uDDDC\uDECF\uDECB\uDD49]\uFE0F?|[\uDE00\uDE03\uDE04\uDE01\uDE06\uDE05\uDE02\uDE42\uDE43\uDE09\uDE0A\uDE07\uDE0D\uDE18\uDE17\uDE1A\uDE19\uDE0B\uDE1B-\uDE1D\uDE10\uDE11\uDE36\uDE0F\uDE12\uDE44\uDE2C\uDE0C\uDE14\uDE2A\uDE34\uDE37\uDE35\uDE0E\uDE15\uDE1F\uDE41\uDE2E\uDE2F\uDE32\uDE33\uDE26-\uDE28\uDE30\uDE25\uDE22\uDE2D\uDE31\uDE16\uDE23\uDE1E\uDE13\uDE29\uDE2B\uDE24\uDE21\uDE20\uDE08\uDC7F\uDC80\uDCA9\uDC79-\uDC7B\uDC7D\uDC7E\uDE3A\uDE38\uDE39\uDE3B-\uDE3D\uDE40\uDE3F\uDE3E\uDE48-\uDE4A\uDC8B\uDC8C\uDC98\uDC9D\uDC96\uDC97\uDC93\uDC9E\uDC95\uDC9F\uDC94\uDC9B\uDC9A\uDC99\uDC9C\uDDA4\uDCAF\uDCA2\uDCA5\uDCAB\uDCA6\uDCA8\uDCA3\uDCAC\uDCAD\uDCA4\uDC40\uDC45\uDC44\uDC8F\uDC91\uDC6A\uDC64\uDC65\uDC63\uDC35\uDC12\uDC36\uDC29\uDC3A\uDC31\uDC08\uDC2F\uDC05\uDC06\uDC34\uDC0E\uDC2E\uDC02-\uDC04\uDC37\uDC16\uDC17\uDC3D\uDC0F\uDC11\uDC10\uDC2A\uDC2B\uDC18\uDC2D\uDC01\uDC00\uDC39\uDC30\uDC07\uDC3B\uDC28\uDC3C\uDC3E\uDC14\uDC13\uDC23-\uDC27\uDC38\uDC0A\uDC22\uDC0D\uDC32\uDC09\uDC33\uDC0B\uDC2C\uDC1F-\uDC21\uDC19\uDC1A\uDC0C\uDC1B-\uDC1E\uDC90\uDCAE\uDD2A\uDDFE\uDDFB\uDC92\uDDFC\uDDFD\uDD4C\uDED5\uDD4D\uDD4B\uDC88\uDE82-\uDE8A\uDE9D\uDE9E\uDE8B-\uDE8E\uDE90-\uDE9C\uDEF5\uDEFA\uDEB2\uDEF4\uDEF9\uDE8F\uDEA8\uDEA5\uDEA6\uDED1\uDEA7\uDEF6\uDEA4\uDEA2\uDEEB\uDEEC\uDCBA\uDE81\uDE9F-\uDEA1\uDE80\uDEF8\uDD5B\uDD67\uDD50\uDD5C\uDD51\uDD5D\uDD52\uDD5E\uDD53\uDD5F\uDD54\uDD60\uDD55\uDD61\uDD56\uDD62\uDD57\uDD63\uDD58\uDD64\uDD59\uDD65\uDD5A\uDD66\uDD25\uDCA7\uDEF7\uDD2E\uDC53-\uDC62\uDC51\uDC52\uDCFF\uDC84\uDC8D\uDC8E\uDD07-\uDD0A\uDCE2\uDCE3\uDCEF\uDD14\uDD15\uDCFB\uDCF1\uDCF2\uDCDE-\uDCE0\uDD0B\uDD0C\uDCBB\uDCBD-\uDCC0\uDCFA\uDCF7-\uDCF9\uDCFC\uDD0D\uDD0E\uDCA1\uDD26\uDCD4-\uDCDA\uDCD3\uDCD2\uDCC3\uDCDC\uDCC4\uDCF0\uDCD1\uDD16\uDCB0\uDCB4-\uDCB8\uDCB3\uDCB9\uDCB1\uDCB2\uDCE7-\uDCE9\uDCE4-\uDCE6\uDCEB\uDCEA\uDCEC-\uDCEE\uDCDD\uDCBC\uDCC1\uDCC2\uDCC5-\uDCD0\uDD12\uDD13\uDD0F-\uDD11\uDD28\uDD2B\uDD27\uDD29\uDD17\uDD2C\uDD2D\uDCE1\uDC89\uDC8A\uDEAA\uDEBD\uDEBF\uDEC1\uDED2\uDEAC\uDDFF\uDEAE\uDEB0\uDEB9-\uDEBC\uDEBE\uDEC2-\uDEC5\uDEB8\uDEAB\uDEB3\uDEAD\uDEAF\uDEB1\uDEB7\uDCF5\uDD1E\uDD03\uDD04\uDD19-\uDD1D\uDED0\uDD4E\uDD2F\uDD00-\uDD02\uDD3C\uDD3D\uDD05\uDD06\uDCF6\uDCF3\uDCF4\uDD31\uDCDB\uDD30\uDD1F-\uDD24\uDD34\uDFE0-\uDFE2\uDD35\uDFE3-\uDFE5\uDFE7-\uDFE9\uDFE6\uDFEA\uDFEB\uDD36-\uDD3B\uDCA0\uDD18\uDD33\uDD32\uDEA9])|\uD83E(?:[\uDD1A\uDD0F\uDD1E\uDD1F\uDD18\uDD19\uDD1B\uDD1C\uDD32\uDD33\uDDB5\uDDB6\uDDBB\uDDD2](?:\uD83C[\uDFFB-\uDFFF])?|\uDDD1(?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?))?)|\u200D(?:\uD83E(?:\uDD1D\u200D\uD83E\uDDD1|[\uDDB0\uDDB1\uDDB3\uDDB2\uDDAF\uDDBC\uDDBD])|\u2695\uFE0F?|\uD83C[\uDF93\uDFEB\uDF3E\uDF73\uDFED\uDFA4\uDFA8]|\u2696\uFE0F?|\uD83D[\uDD27\uDCBC\uDD2C\uDCBB\uDE80\uDE92]|\u2708\uFE0F?)))?|[\uDDD4\uDDD3](?:\uD83C[\uDFFB-\uDFFF])?|[\uDDCF\uDD26\uDD37](?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\uDD34\uDDD5\uDD35\uDD30\uDD31\uDD36](?:\uD83C[\uDFFB-\uDFFF])?|[\uDDB8\uDDB9\uDDD9-\uDDDD](?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\uDDDE\uDDDF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?|[\uDDCD\uDDCE\uDDD6\uDDD7\uDD38](?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|\uDD3C(?:\u200D(?:[\u2642\u2640]\uFE0F?))?|[\uDD3D\uDD3E\uDD39\uDDD8](?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\uDD23\uDD70\uDD29\uDD2A\uDD11\uDD17\uDD2D\uDD2B\uDD14\uDD10\uDD28\uDD25\uDD24\uDD12\uDD15\uDD22\uDD2E\uDD27\uDD75\uDD76\uDD74\uDD2F\uDD20\uDD73\uDD13\uDDD0\uDD7A\uDD71\uDD2C\uDD21\uDD16\uDDE1\uDD0E\uDD0D\uDD1D\uDDBE\uDDBF\uDDE0\uDDB7\uDDB4\uDD3A\uDDB0\uDDB1\uDDB3\uDDB2\uDD8D\uDDA7\uDDAE\uDD8A\uDD9D\uDD81\uDD84\uDD93\uDD8C\uDD99\uDD92\uDD8F\uDD9B\uDD94\uDD87\uDDA5\uDDA6\uDDA8\uDD98\uDDA1\uDD83\uDD85\uDD86\uDDA2\uDD89\uDDA9\uDD9A\uDD9C\uDD8E\uDD95\uDD96\uDD88\uDD8B\uDD97\uDD82\uDD9F\uDDA0\uDD40\uDD6D\uDD5D\uDD65\uDD51\uDD54\uDD55\uDD52\uDD6C\uDD66\uDDC4\uDDC5\uDD5C\uDD50\uDD56\uDD68\uDD6F\uDD5E\uDDC7\uDDC0\uDD69\uDD53\uDD6A\uDD59\uDDC6\uDD5A\uDD58\uDD63\uDD57\uDDC8\uDDC2\uDD6B\uDD6E\uDD5F-\uDD61\uDD80\uDD9E\uDD90\uDD91\uDDAA\uDDC1\uDD67\uDD5B\uDD42\uDD43\uDD64\uDDC3\uDDC9\uDDCA\uDD62\uDD44\uDDED\uDDF1\uDDBD\uDDBC\uDE82\uDDF3\uDE90\uDDE8\uDDE7\uDD47-\uDD49\uDD4E\uDD4F\uDD4D\uDD4A\uDD4B\uDD45\uDD3F\uDD4C\uDE80\uDE81\uDDFF\uDDE9\uDDF8\uDDF5\uDDF6\uDD7D\uDD7C\uDDBA\uDDE3-\uDDE6\uDD7B\uDE71-\uDE73\uDD7E\uDD7F\uDE70\uDDE2\uDE95\uDD41\uDDEE\uDE94\uDDFE\uDE93\uDDAF\uDDF0\uDDF2\uDDEA-\uDDEC\uDE78-\uDE7A\uDE91\uDE92\uDDF4\uDDF7\uDDF9-\uDDFD\uDDEF])|[\u263A\u2639\u2620\u2763\u2764]\uFE0F?|\u270B(?:\uD83C[\uDFFB-\uDFFF])?|[\u270C\u261D](?:(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F))?|\u270A(?:\uD83C[\uDFFB-\uDFFF])?|\u270D(?:(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F))?|\uD83C(?:\uDF85(?:\uD83C[\uDFFB-\uDFFF])?|\uDFC3(?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\uDFC7\uDFC2](?:\uD83C[\uDFFB-\uDFFF])?|\uDFCC(?:(?:\uFE0F(?:\u200D(?:[\u2642\u2640]\uFE0F?))?|\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\uDFC4\uDFCA](?:(?:\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|\uDFCB(?:(?:\uFE0F(?:\u200D(?:[\u2642\u2640]\uFE0F?))?|\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\uDFF5\uDF36\uDF7D\uDFD4-\uDFD6\uDFDC-\uDFDF\uDFDB\uDFD7\uDFD8\uDFDA\uDFD9\uDFCE\uDFCD\uDF21\uDF24-\uDF2C\uDF97\uDF9F\uDF96\uDF99-\uDF9B\uDF9E\uDFF7\uDD70\uDD71\uDD7E\uDD7F\uDE02\uDE37]\uFE0F?|\uDFF4(?:(?:\u200D\u2620\uFE0F?|\uDB40\uDC67\uDB40\uDC62\uDB40(?:\uDC65\uDB40\uDC6E\uDB40\uDC67\uDB40\uDC7F|\uDC73\uDB40\uDC63\uDB40\uDC74\uDB40\uDC7F|\uDC77\uDB40\uDC6C\uDB40\uDC73\uDB40\uDC7F)))?|\uDFF3(?:(?:\uFE0F(?:\u200D\uD83C\uDF08)?|\u200D\uD83C\uDF08))?|\uDDE6\uD83C[\uDDE8-\uDDEC\uDDEE\uDDF1\uDDF2\uDDF4\uDDF6-\uDDFA\uDDFC\uDDFD\uDDFF]|\uDDE7\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEF\uDDF1-\uDDF4\uDDF6-\uDDF9\uDDFB\uDDFC\uDDFE\uDDFF]|\uDDE8\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDEE\uDDF0-\uDDF5\uDDF7\uDDFA-\uDDFF]|\uDDE9\uD83C[\uDDEA\uDDEC\uDDEF\uDDF0\uDDF2\uDDF4\uDDFF]|\uDDEA\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDED\uDDF7-\uDDFA]|\uDDEB\uD83C[\uDDEE-\uDDF0\uDDF2\uDDF4\uDDF7]|\uDDEC\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEE\uDDF1-\uDDF3\uDDF5-\uDDFA\uDDFC\uDDFE]|\uDDED\uD83C[\uDDF0\uDDF2\uDDF3\uDDF7\uDDF9\uDDFA]|\uDDEE\uD83C[\uDDE8-\uDDEA\uDDF1-\uDDF4\uDDF6-\uDDF9]|\uDDEF\uD83C[\uDDEA\uDDF2\uDDF4\uDDF5]|\uDDF0\uD83C[\uDDEA\uDDEC-\uDDEE\uDDF2\uDDF3\uDDF5\uDDF7\uDDFC\uDDFE\uDDFF]|\uDDF1\uD83C[\uDDE6-\uDDE8\uDDEE\uDDF0\uDDF7-\uDDFB\uDDFE]|\uDDF2\uD83C[\uDDE6\uDDE8-\uDDED\uDDF0-\uDDFF]|\uDDF3\uD83C[\uDDE6\uDDE8\uDDEA-\uDDEC\uDDEE\uDDF1\uDDF4\uDDF5\uDDF7\uDDFA\uDDFF]|\uDDF4\uD83C\uDDF2|\uDDF5\uD83C[\uDDE6\uDDEA-\uDDED\uDDF0-\uDDF3\uDDF7-\uDDF9\uDDFC\uDDFE]|\uDDF6\uD83C\uDDE6|\uDDF7\uD83C[\uDDEA\uDDF4\uDDF8\uDDFA\uDDFC]|\uDDF8\uD83C[\uDDE6-\uDDEA\uDDEC-\uDDF4\uDDF7-\uDDF9\uDDFB\uDDFD-\uDDFF]|\uDDF9\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDED\uDDEF-\uDDF4\uDDF7\uDDF9\uDDFB\uDDFC\uDDFF]|\uDDFA\uD83C[\uDDE6\uDDEC\uDDF2\uDDF3\uDDF8\uDDFE\uDDFF]|\uDDFB\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDEE\uDDF3\uDDFA]|\uDDFC\uD83C[\uDDEB\uDDF8]|\uDDFD\uD83C\uDDF0|\uDDFE\uD83C[\uDDEA\uDDF9]|\uDDFF\uD83C[\uDDE6\uDDF2\uDDFC]|[\uDFFB-\uDFFF\uDF38-\uDF3C\uDF37\uDF31-\uDF35\uDF3E-\uDF43\uDF47-\uDF53\uDF45\uDF46\uDF3D\uDF44\uDF30\uDF5E\uDF56\uDF57\uDF54\uDF5F\uDF55\uDF2D-\uDF2F\uDF73\uDF72\uDF7F\uDF71\uDF58-\uDF5D\uDF60\uDF62-\uDF65\uDF61\uDF66-\uDF6A\uDF82\uDF70\uDF6B-\uDF6F\uDF7C\uDF75\uDF76\uDF7E\uDF77-\uDF7B\uDF74\uDFFA\uDF0D-\uDF10\uDF0B\uDFE0-\uDFE6\uDFE8-\uDFED\uDFEF\uDFF0\uDF01\uDF03-\uDF07\uDF09\uDFA0-\uDFA2\uDFAA\uDF11-\uDF20\uDF0C\uDF00\uDF08\uDF02\uDF0A\uDF83\uDF84\uDF86-\uDF8B\uDF8D-\uDF91\uDF80\uDF81\uDFAB\uDFC6\uDFC5\uDFC0\uDFD0\uDFC8\uDFC9\uDFBE\uDFB3\uDFCF\uDFD1-\uDFD3\uDFF8\uDFA3\uDFBD\uDFBF\uDFAF\uDFB1\uDFAE\uDFB0\uDFB2\uDCCF\uDC04\uDFB4\uDFAD\uDFA8\uDF92\uDFA9\uDF93\uDFBC\uDFB5\uDFB6\uDFA4\uDFA7\uDFB7-\uDFBB\uDFA5\uDFAC\uDFEE\uDFF9\uDFE7\uDFA6\uDD8E\uDD91-\uDD9A\uDE01\uDE36\uDE2F\uDE50\uDE39\uDE1A\uDE32\uDE51\uDE38\uDE34\uDE33\uDE3A\uDE35\uDFC1\uDF8C])|\u26F7\uFE0F?|\u26F9(?:(?:\uFE0F(?:\u200D(?:[\u2642\u2640]\uFE0F?))?|\uD83C(?:[\uDFFB-\uDFFF](?:\u200D(?:[\u2642\u2640]\uFE0F?))?)|\u200D(?:[\u2642\u2640]\uFE0F?)))?|[\u2618\u26F0\u26E9\u2668\u26F4\u2708\u23F1\u23F2\u2600\u2601\u26C8\u2602\u26F1\u2744\u2603\u2604\u26F8\u2660\u2665\u2666\u2663\u265F\u26D1\u260E\u2328\u2709\u270F\u2712\u2702\u26CF\u2692\u2694\u2699\u2696\u26D3\u2697\u26B0\u26B1\u26A0\u2622\u2623\u2B06\u2197\u27A1\u2198\u2B07\u2199\u2B05\u2196\u2195\u2194\u21A9\u21AA\u2934\u2935\u269B\u2721\u2638\u262F\u271D\u2626\u262A\u262E\u25B6\u23ED\u23EF\u25C0\u23EE\u23F8-\u23FA\u23CF\u2640\u2642\u2695\u267E\u267B\u269C\u2611\u2714\u2716\u303D\u2733\u2734\u2747\u203C\u2049\u3030\u00A9\u00AE\u2122]\uFE0F?|[\u0023\u002A\u0030-\u0039](?:\uFE0F\u20E3|\u20E3)|[\u2139\u24C2\u3297\u3299\u25FC\u25FB\u25AA\u25AB]\uFE0F?|[\u2615\u26EA\u26F2\u26FA\u26FD\u2693\u26F5\u231B\u23F3\u231A\u23F0\u2B50\u26C5\u2614\u26A1\u26C4\u2728\u26BD\u26BE\u26F3\u267F\u26D4\u2648-\u2653\u26CE\u23E9-\u23EC\u2B55\u2705\u274C\u274E\u2795-\u2797\u27B0\u27BF\u2753-\u2755\u2757\u26AB\u26AA\u2B1B\u2B1C\u25FE\u25FD])"
                , RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
            List<ArtTheme> themePool = GetThemePoolOrdered().Select(x => x.Item2).ToList();
            if (themePool.Count > 0)
            {
                reply += $"\n({string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, "add theme <theme name>", "add a theme into the poll")})";
                //Encoding unicode = Encoding.Unicode;
                //byte[] bytes = new byte[] { 48, 0, 227, 32 }; // ::zero::

                foreach (ArtTheme artTheme in themePool)
                {
                    //var bytes = BitConverter.GetBytes(emojiNumber);
                    //var emojistr2 = "\u0030\u20E3";
                    //var bytes2 = unicode.GetBytes(emojistr2);
                    //var bytes2int32u = BitConverter.ToUInt32(bytes2, 0);

                    //var emojiCode = unicode.GetString(bytes);
                    //bytes[0] += 1;

                    string emojiCode = artTheme.EmojiCode;
                    if (string.IsNullOrWhiteSpace(emojiCode))
                        return false;

                    emojiObjs.Add(new Emoji(emojiCode));
                    reply += $"\n{emojiCode} : `{artTheme.Theme}`";
                }
            }
            else
            {
                reply += $"\n({string.Format(Properties.Resources.GLOBAL_EMPTY, "themes")}, {string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, "add theme <theme name>", "add a theme into the poll")})";
            }

            var msg = await channel.SendMessageAsync(embed: Utils.EmbedMessage(client, reply, Utils.Emotion.positive));
            xs.Settings.SetThemePollID(msg.Id);
            emojiObjs.ForEach(async e => await msg.AddReactionAsync(e));
            await EditMessagePin(client, msg.Id, true /*pin*/);

            await NotifySubscribers(client, "the `entry week` started, and a `theme poll` is currently taking place in the teanicorn art trade channel");
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

            var restEmbed = restMsg.Embeds.SingleOrDefault();
            if (restEmbed == default)
                return false;

            List<Emoji> emojiObjs = new List<Emoji>();
            var reply = $"{string.Format(Properties.Resources.TRADE_THEME_POOL_START)}";
            List<ArtTheme> themePool = GetThemePoolOrdered().Select(x => x.Item2).ToList();
            if (themePool.Count > 0)
            {
                reply += $"\n({string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, "add theme <theme name>", "add a theme into the poll")})";

                foreach (string line in restEmbed.Description.Split('\n'))
                {
                    string[] themeLine = line.Split(':');
                    if (themeLine.Count() != 2)
                        continue;

                    string contentTheme = themeLine[1].Replace('`', ' ').ToLower().Trim();
                    if (themePool.FirstOrDefault(x => x.Theme == contentTheme) == default)
                        continue;

                    string contentEmojiCode = themeLine[0].ToLower().Trim();
                    if (!Utils.EmojiPattern.Match(contentEmojiCode).Success)
                        continue;

                    emojiObjs.Add(new Emoji(contentEmojiCode));

                    reply += $"\n{line}";

                    themePool.RemoveAll(x => x.Theme == contentTheme);
                }

                foreach (ArtTheme artTheme in themePool)
                {
                    string emojiCode = artTheme.EmojiCode;
                    if (string.IsNullOrWhiteSpace(emojiCode))
                        return false;

                    reply += $"\n{emojiCode} : `{artTheme.Theme}`";
                    emojiObjs.Add(new Emoji(emojiCode));
                }
            }
            else
            {
                reply += $"\n({string.Format(Properties.Resources.GLOBAL_EMPTY, "themes")}, {string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, "add theme <theme name>", "add a theme into the poll")})";
            }

            await restMsg.ModifyAsync(x => x.Embed = Utils.EmbedMessage(client, reply, Utils.Emotion.positive));
            foreach (var emoji in restMsg.Reactions)
            {
                string reactionCode = emoji.Key.Name;
                var foundEmoji = emojiObjs.FirstOrDefault(x => x.Name == reactionCode);
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
                    await su.SendMessageAsync(embed: Utils.EmbedMessage(client, string.Format(Properties.Resources.SUBSCRIBERS_NOTICE, userId, message, Config.CmdPrefix, "subscribe false"), Utils.Emotion.positive));
            }
        }

        public static EmbedBuilder GetFooterBuilder(DiscordSocketClient client, string message = "", string imageUrl = "", string emote = "")
        {
            SocketTextChannel channel = FindChannel(client, xs.Settings.GetWorkingChannel());
            if (channel == null)
                return null;

            List<string> footer = new List<string>();

            if (xs.Settings.GetThemePollMessageId() != 0)
                footer.Add($"[theme poll](https://discordapp.com/channels/{FindGuild(client).Id}/{channel.Id}/{xs.Settings.GetThemePollMessageId()})");

            if (xs.Settings.GetHelpMessageId() != 0)
                footer.Add($"[bot help](https://discordapp.com/channels/{FindGuild(client).Id}/{channel.Id}/{xs.Settings.GetHelpMessageId()})");

            if (xs.Settings.GetNaughtyListMessageId() != 0)
                footer.Add($"[naughty list](https://discordapp.com/channels/{FindGuild(client).Id}/{channel.Id}/{xs.Settings.GetNaughtyListMessageId()})");

            footer.Add($"[teanicorn web](https://teanicorns.weebly.com/)");

            //string avatarUrl = client.CurrentUser.GetAvatarUrl();
            //if (string.IsNullOrEmpty(avatarUrl))
            //avatarUrl = client.CurrentUser.GetDefaultAvatarUrl();

            //var emotes = Context.Guild.Emotes;

            return new EmbedBuilder()
                .WithColor(51, 144, 243)
                .WithDescription($"{message} {emote}\n\n{string.Join(" **|** ", footer)}")
                .WithImageUrl(imageUrl);
        }

        public static Embed EmbedFooter(DiscordSocketClient client, string message = "", string imageUrl = "", string emote = "")
        {
            return GetFooterBuilder(client, message, imageUrl, emote)?.Build();
        }

        public enum Emotion
        {
            none = 0,
            positive = 1,
            neutral = 2,
            negative = 3
        }

        public static string GetRandomEmote(DiscordSocketClient client, Emotion emotionType)
        {
            var emotes = Config.GetEmotions(emotionType);
            if (emotes == null)
                return "";

            var guild = FindGuild(client);
            if (guild == null)
                return "";

            var emoteName = emotes[rng.Next(emotes.Length)];
            if (string.IsNullOrWhiteSpace(emoteName))
                return "";

            Emote emote = guild.Emotes.FirstOrDefault(x => x.Name.IndexOf(emoteName, StringComparison.OrdinalIgnoreCase) != -1);
            if (emote == null)
                return "";

            return $"<:{emote.Name}:{emote.Id}>";
        }

        public static Embed EmbedMessage(DiscordSocketClient client, string message, Emotion emotionType = Emotion.none, string imageUrl = "")
        {
            return EmbedFooter(client, message, imageUrl, GetRandomEmote(client, emotionType));
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
            ret.Add($"{string.Format(Properties.Resources.INFO_INTRO, DiscordConfig.Version, Config.CmdPrefix, "about", "set entry")}");
            string about = $"**User Commands**";
            string adminAbout = adminUser ? $"**Admin commands**" : "";

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
                        adminAbout += $"\n{aliases} : {cmd.Summary}";
                }
                else
                    about += $"\n{aliases} : {cmd.Summary}";
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
            var helpMsg = await channel.SendMessageAsync(embed: Utils.EmbedMessage(client, $"@everyone {aboutMsgs[(int)AboutMessageSubtype.intro]}\n\n{aboutMsgs[(int)AboutMessageSubtype.userCommands]}", Utils.Emotion.positive));
            xs.Settings.SetHelpMessageId(helpMsg.Id);

            await EditMessagePin(client, helpMsg.Id, true /*pin*/);
            return true;
        }

        public static ulong ExtractUserMention(ref string message)
        {
            Match match = new Regex("<@!\\d+>").Match(message);
            if (match.Success)
            {
                message = string.Join(' ', message.Split(match.Value).Select(x => x.Trim())).Trim();
                return MentionUtils.ParseUser(match.Value);
            }

            return 0;
        }

        public static async Task<bool> SendPartnerResponse(DiscordSocketClient client, UserData entry, string theme, bool bNotifyChannel = false)
        {
            if (string.IsNullOrWhiteSpace(entry.ArtUrl))
                return false;

            UserData nextEntry;
            if (!xs.Entries.Next(entry.UserId, out nextEntry))
                return false;

            SocketUser nextUser = client.GetUser(nextEntry.UserId);
            if (nextUser == null)
                return false;

            return await Modules.ReferenceModule.SendPartnerArtResponse(client, entry, nextUser, theme, bNotifyChannel);
        }

        public static ApplicationData GetAppDataFromHistory(int level)
        {
            return xs.History.GetTrade(level);
        }

        public static List<UserData> GetMissingArt(ApplicationData appData)
        {
            List<UserData> ret = new List<UserData>();
            if (appData == null)
                return ret;

            foreach (UserData x in appData.GetStorage())
            {
                if (string.IsNullOrWhiteSpace(x.ArtUrl))
                    ret.Add(x);
            }
            return ret;
        }

        public static List<UserData> GetMissingArt(int? level = null)
        {
            if (!level.HasValue)
                return GetMissingArt(xs.Entries);
            return GetMissingArt(GetAppDataFromHistory(level.Value));
        }

        public static string GetMissingArtToStr(ApplicationData appData)
        {
            if (appData == null)
                return "";

            List<UserData> userDataList = GetMissingArt(appData);
            if (userDataList == null)
                return "";

            return string.Join(", ", userDataList.Select(x => string.IsNullOrWhiteSpace(x.NickName) ? x.UserName : x.NickName));
        }

        public static async Task<bool> CreateOrEditNaughtyList(DiscordSocketClient client, string channelName, SocketUser userEdit = null)
        {
            channelName = channelName.Trim();
            if (string.IsNullOrWhiteSpace(channelName))
                channelName = xs.Settings.GetWorkingChannel();

            SocketTextChannel channel = FindChannel(client, channelName);
            if (channel == null)
                return false;

            string artMissing = "";
            ApplicationData artHistory0 = GetAppDataFromHistory(0);
            if (artHistory0 != null)
                artMissing = GetMissingArtToStr(artHistory0);

            string artMissingHistory1 = "";
            ApplicationData artHistory1 = GetAppDataFromHistory(1);
            if (artHistory1 != null)
                artMissingHistory1 = GetMissingArtToStr(artHistory1);

            string artMissingHistory2 = "";
            ApplicationData artHistory2 = GetAppDataFromHistory(2);
            if (artHistory2 != null)
                artMissingHistory2 = GetMissingArtToStr(artHistory2);

            string naughtyList = "";
            if (!string.IsNullOrWhiteSpace(artMissing))
                naughtyList += $"\n`{artHistory0.GetTheme()}` : {artMissing}";
            if (!string.IsNullOrWhiteSpace(artMissingHistory1))
                naughtyList += $"\n`{artHistory1.GetTheme()}` : {artMissingHistory1}";
            if (!string.IsNullOrWhiteSpace(artMissingHistory2))
                naughtyList += $"\n`{artHistory2.GetTheme()}` : {artMissingHistory2}";

            var reply = Utils.EmbedMessage(client, $"**Naughty List**\nif you are on the list {string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, $"reveal art <theme name>", "register the missing art for the listed themes:")}{naughtyList}", Utils.Emotion.negative);

            if (userEdit != null)
            {
                if (artHistory0.Get(userEdit.Id) == null && artHistory1.Get(userEdit.Id) == null && artHistory2.Get(userEdit.Id) == null)
                    return true;

                var naughtyMsg = await FindChannelMessage(client, xs.Settings.GetNaughtyListMessageId());
                if (naughtyMsg == null)
                    return false;

                var restMsg = naughtyMsg as Discord.Rest.RestUserMessage;
                if (restMsg == null)
                    return false;

                if (string.IsNullOrWhiteSpace(naughtyList))
                {
                    await EditMessagePin(client, xs.Settings.GetNaughtyListMessageId(), false /*unpin*/);
                    await naughtyMsg.DeleteAsync();
                    xs.Settings.SetNaughtyListMessageId(0);
                }
                else
                {
                    await restMsg.ModifyAsync(x => x.Embed = reply);
                }
            }
            else if (!string.IsNullOrWhiteSpace(naughtyList))
            {
                await EditMessagePin(client, xs.Settings.GetNaughtyListMessageId(), false /*unpin*/);

                var naughtyMsg = await channel.SendMessageAsync(embed: reply);
                xs.Settings.SetNaughtyListMessageId(naughtyMsg.Id);

                await EditMessagePin(client, naughtyMsg.Id, true /*pin*/);
            }

            return true;
        }

        public static async Task<string> CleanupWrongChannelMessage(SocketCommandContext ctx, string msg)
        {
            if (ctx.IsPrivate)
                return "";
            
            await ctx.Message.DeleteAsync();
            return msg;
        }
    }
}
