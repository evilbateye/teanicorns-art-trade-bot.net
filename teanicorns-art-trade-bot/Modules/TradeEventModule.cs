using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord;
using Discord.WebSocket;
using System.Text;

namespace teanicorns_art_trade_bot.Modules
{
    //[RequireUserPermissionAttribute(GuildPermission.Administrator)]
    //[Group(Utils.adminGroupId)]
    public class TradeEventModule : ModuleBase<SocketCommandContext>
    {
        public static List<Storage.UserData> GetMissingArt(Storage.ApplicationData appData)
        {
            List<Storage.UserData> ret = new List<Storage.UserData>();
            if (appData == null)
                return ret;

            foreach (Storage.UserData x in appData.GetStorage())
            {
                if (string.IsNullOrWhiteSpace(x.ArtUrl))
                    ret.Add(x);
            }
            return ret;
        }

        public static Storage.ApplicationData GetAppDataFromHistory(int level)
        {
            if (level < Storage.Axx.AppHistory.Count())
                return Storage.Axx.AppHistory.History[level];
            return null;
        }
        public static List<Storage.UserData> GetMissingArt(int? level = null)
        {
            if (!level.HasValue)
                return GetMissingArt(Storage.Axx.AppData);
            return GetMissingArt(GetAppDataFromHistory(level.Value));
        }

        public static string GetMissingArtToStr(Storage.ApplicationData appData)
        {
            if (appData == null)
                return "";

            List<Storage.UserData> userDataList = GetMissingArt(appData);
            if (userDataList == null)
                return "";

            return string.Join(", ", userDataList.Select(x => string.IsNullOrWhiteSpace(x.NickName) ? x.UserName : x.NickName));
        }

        public static async Task StartEntryWeek(DiscordSocketClient client, double? days2end = null, bool? force = null, [Remainder]string theme = null)
        {
            Storage.Axx.AppHistory.RecordTrade(Storage.Axx.AppData);
            await GoogleDriveHandler.UploadGoogleFile(Storage.Axx.AppHistoryFileName);
            Storage.Axx.ClearStorage(Storage.Axx.AppData);
            Storage.Axx.AppSettings.ActivateTrade(false, null/*days2start*/, days2end, force);

            if (string.IsNullOrWhiteSpace(theme))
                Storage.Axx.AppData.SetTheme("");
            else
                Storage.Axx.AppData.SetTheme(theme);

            SocketTextChannel channel = Utils.FindChannel(client, Storage.Axx.AppSettings.WorkingChannel);
            if (channel != null)
            {
                string artMissing = "";
                Storage.ApplicationData artHistory0 = GetAppDataFromHistory(0);
                if (artHistory0 != null)
                    artMissing = GetMissingArtToStr(artHistory0);

                string artMissingHistory1 = "";
                Storage.ApplicationData artHistory1 = GetAppDataFromHistory(1);
                if (artHistory1 != null)
                    artMissingHistory1 = GetMissingArtToStr(artHistory1);

                string artMissingHistory2 = "";
                Storage.ApplicationData artHistory2 = GetAppDataFromHistory(2);
                if (artHistory2 != null)
                    artMissingHistory2 = GetMissingArtToStr(artHistory2);

                await channel.SendMessageAsync(string.Format(Properties.Resources.TRADE_NEW_ENTRIES, Config.CmdPrefix, "set entry", "about") + "\n"
                    + (string.IsNullOrWhiteSpace(Storage.Axx.AppData.Theme) ? "" : string.Format(Properties.Resources.TRADE_THIS_THEME, Storage.Axx.AppData.Theme) + "\n")
                    + (string.IsNullOrWhiteSpace(artMissing) ? string.Format(Properties.Resources.TRADE_ART_ON_TIME) : string.Format(Properties.Resources.TRADE_ART_LATE, artMissing))
                    + (string.IsNullOrWhiteSpace(artMissingHistory1) ? "" : "\n" + string.Format(Properties.Resources.TRADE_ART_LATE_1, artHistory1.Theme, artMissingHistory1))
                    + (string.IsNullOrWhiteSpace(artMissingHistory2) ? "" : "\n" + string.Format(Properties.Resources.TRADE_ART_LATE_2, artHistory2.Theme, artMissingHistory2))
                    );

                foreach (Storage.UserData user in GetMissingArt(artHistory0))
                {
                    SocketUser su = client.GetUser(user.UserId);
                    if (su != null)
                        await su.SendMessageAsync(string.Format(Properties.Resources.TRADE_ART_LATE_DM, user.UserId, artHistory0.Theme));
                }
            }
        }

        [Command("entry week")]
        [Alias("ew")]
        [Summary("stops the art trade, clears all entries and theme, starts accepting new entries")]
        [InfoModule.SummaryDetail("if an art trade is currently taking place, it is stopped and entry week is started automatically" +
            "\nthe finished trade is recorded into trade history" +
            "\nafter that the entries and trade theme are cleared" +
            "\nlist of members that did not submit their art on time is printed into the working channel (dating 3 trades back)" +
            "\nthe latest members with missing art are contacted using a direct message")]
        public async Task EntryWeek([Summary("number of days until the next trade ends (the duration of the trade month) (optional)")]double? days2end = null
            , [Summary("bool flag indicating if the next trade should be forced to end automatically at the end (optional)")]bool? force = null
            , [Summary("theme that will be set for the next art trade (optional)")][Remainder]string theme = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppData);
            Storage.Axx.BackupStorage(Storage.Axx.AppSettings);
            Storage.Axx.BackupStorage(Storage.Axx.AppHistory);

            if (Storage.Axx.AppSettings.ArtTradeActive != false)
                await StartEntryWeek(Context.Client, days2end, force, theme);
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_IN_PROGRESS, user.Id, "entry week"));
        }


        public async Task<bool> CreateThemePoll()
        {
            var themePool = Storage.Axx.AppData.GetStorage().SelectMany(x => x.ThemePool).ToList();
            if (themePool.Count == 0)
                return false;
            if (themePool.Count > 10)
                themePool.RemoveRange(10, themePool.Count - 10);

            var reply = $"\n{string.Format(Properties.Resources.TRADE_THEME_POOL_START)}\n";
            Encoding unicode = Encoding.Unicode;
            //byte[] bytes = new byte[] { 48, 0, 227, 32 }; // ::zero::
            List<Emoji> emojiObjs = new List<Emoji>();
                        
            for (int i = 0; i < themePool.Count; ++i)
            {
                //var bytes = BitConverter.GetBytes(emojiNumber);
                //var emojistr2 = "\u0030\u20E3";
                //var bytes2 = unicode.GetBytes(emojistr2);
                //var bytes2int32u = BitConverter.ToUInt32(bytes2, 0);

                //var emojiCode = unicode.GetString(bytes);
                //bytes[0] += 1;

                string theme = themePool[i];
                string emojiCode = Utils.EmojiCodes[i];
                emojiObjs.Add(new Emoji(emojiCode));
                reply += $"\n{emojiCode} : `{theme}`";
            }
            

            SocketTextChannel channel = Utils.FindChannel(Context.Client, Storage.Axx.AppSettings.WorkingChannel);
            if (channel != null)
            {
                var msg = await channel.SendMessageAsync(reply);
                foreach (var emoji in emojiObjs)
                    await msg.AddReactionAsync(emoji);
                Storage.Axx.AppSettings.SetThemePollID(msg.Id);
                return true;
            }

            return false;
        }

        [Command("trade month")]
        [Alias("tm")]
        [Summary("starts the art trade, shuffles entries, sends all their partners in a DM, stops accepting entries")]
        [InfoModule.SummaryDetail("if an entry week is currently taking place, it is stopped and trade month is started" +
            "\nthe entered entries are randomly shuffled generating a chain" +
            "\nthe chain goes only one way, meaning that for each entry there is a next entry, and the next entry is the first entry's partner" +
            "\nbut the first entry does not see it's previous entry, so they do not know who has them as their partner" +
            "\na direct message is sent to each participant containing their partner's information")]
        public async Task TradeMonth([Summary("number of days until the next trade ends (the duration of the trade month) (optional)")]double? days2end = null
            , [Summary("bool flag indicating if the next trade should be forced to end automatically at the end (optional)")]bool? force = null
            , [Summary("theme that will be set for the next art trade (optional)")][Remainder]string theme = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppData);
            Storage.Axx.BackupStorage(Storage.Axx.AppSettings);

            if (Storage.Axx.AppSettings.ArtTradeActive != true)
            {
                Storage.Axx.AppSettings.ActivateTrade(true, 0.0/*days2start*/, days2end, force);
                if (!string.IsNullOrWhiteSpace(theme))
                    Storage.Axx.AppData.SetTheme(theme);
                Storage.Axx.AppData.Shuffle(Storage.Axx.AppHistory);

                string notification = string.Format(Properties.Resources.TRADE_NO_NEW_ENTRIES, Config.CmdPrefix, "reveal art", "about") + "\n"
                    + (string.IsNullOrWhiteSpace(Storage.Axx.AppData.Theme) ? "" : string.Format(Properties.Resources.TRADE_THIS_THEME, Storage.Axx.AppData.Theme) + "\n")
                    + (Storage.Axx.AppSettings.TradeDays == 0 ? "" : string.Format(Properties.Resources.TRADE_ENDS_ON, Storage.Axx.AppSettings.TradeDays, Storage.Axx.AppSettings.TradeStart.AddDays(Storage.Axx.AppSettings.TradeDays).ToString("dd-MMMM")));

                SocketTextChannel channel = Utils.FindChannel(Context.Client, Storage.Axx.AppSettings.WorkingChannel);
                if (channel != null)
                {
                    await channel.SendMessageAsync(notification);

                    if (string.IsNullOrWhiteSpace(Storage.Axx.AppData.Theme))
                        await CreateThemePoll();

                    await SendPartners();
                }
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_IN_PROGRESS, user.Id, "trade month"));
        }

        [Command("theme")]
        [Alias("th")]
        [Summary("set the art trade theme")]
        public async Task SetTheme([Summary("theme to be set for the next art trade")][Remainder]string theme)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            if (string.IsNullOrWhiteSpace(theme))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppData);

            if (Storage.Axx.AppData.SetTheme(theme))
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
        }

        [Command("settings")]
        [Summary("silently changes the current settings")]
        [InfoModule.SummaryDetail("you can silently turn the art trade on/off" +
            "\nchange the start date of the trade and number of days until the trade ends" +
            "\nyou can also modify the force flag, or change the active trading channel")]
        public async Task ChangeSettings([Summary("bool flag indicating trade start/end")]bool? bStart
            , [Summary("number of days until the next trade starts (the duration of the entry week) (optional)")]double? days2start = null
            , [Summary("number of days until the next trade ends (the duration of the trade month) (optional)")]double? days2end = null
            , [Summary("bool flag indicating if the next trade should be forced to end automatically at the end (optional)")] bool? force = null
            , [Summary("name of the only channel where the art trade bot listens for user input")][Remainder]string channel = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppSettings);
            Storage.Axx.AppSettings.ActivateTrade(bStart, days2start, days2end, force);

            if (!string.IsNullOrWhiteSpace(channel))
                await WorkChannel(channel);
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
        }

        [Command("channel")]
        [Summary("sets the working channel")]
        [InfoModule.SummaryDetail("the art trade bot listens for user input messages only in this channel")]
        public async Task WorkChannel([Summary("name of the channel")][Remainder]string channel)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            if (string.IsNullOrWhiteSpace(channel))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppSettings);

            channel = channel.ToLower().Trim();
            var channelObj = Utils.FindChannel(Utils.FindGuild(user), channel);
            if (channelObj != null && Storage.Axx.AppSettings.SetWorkingChannel(channel))
                await channelObj.SendMessageAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
        }

        [Command("list")]
        [Alias("ls")]
        [Summary("sends you a list of all entries in a DM")]
        [InfoModule.SummaryDetail("sends detailed information about art trade entries" +
            "\nthe information is sent using a direct message, because it contains secrets that should not be visible to other trade participants")]
        public async Task ListAllEntries([Summary("bool flag indicating if a more detailed info should be shown")]bool bAll = false)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            string info = (Storage.Axx.AppSettings.ArtTradeActive ? string.Format(Properties.Resources.TRADE_LIST_ONOFF, "trade month") : string.Format(Properties.Resources.TRADE_LIST_ONOFF, "entry week")) + "\n";
            
            info += string.Format(Properties.Resources.TRADE_THIS_THEME, string.IsNullOrWhiteSpace(Storage.Axx.AppData.Theme) ? "`none`" : Storage.Axx.AppData.Theme) + "\n";

            info += string.Format(Properties.Resources.TRADE_LIST_SETTINGS
                , !string.IsNullOrWhiteSpace(Storage.Axx.AppSettings.WorkingChannel) ? Storage.Axx.AppSettings.WorkingChannel : "empty"
                , Storage.Axx.AppSettings.TradeStart.ToString("dd-MMMM")
                , Storage.Axx.AppSettings.GetTradeEnd().ToString("dd-MMMM")
                , Storage.Axx.AppSettings.TradeDays
                , Storage.Axx.AppSettings.Notified
                , Storage.Axx.AppSettings.ForceTradeEnd) + "\n";

            string entries = $"\n{string.Format(Properties.Resources.TRADE_LIST_ENTRIES, user.Id)}\n";
            if (!bAll)
                entries += string.Join("\n", Storage.Axx.AppData.GetStorage().Select(x => $"{x.UserName}" +
                (string.IsNullOrWhiteSpace(x.NickName) ? "" : $" ({x.NickName})") +
                (string.IsNullOrWhiteSpace(x.ArtUrl) ? " , no art" : "")));
            else
                entries += string.Join("\n", Storage.Axx.AppData.GetStorage().Select(x => $"{x.UserName}: {x.UserId}\n" +
                (string.IsNullOrWhiteSpace(x.NickName) ? "" : $"Nickname: {x.NickName}\n") +
                (string.IsNullOrWhiteSpace(x.ReferenceUrl) ? "" : $"Entry Url: <{x.ReferenceUrl}>\n") +
                (string.IsNullOrWhiteSpace(x.ReferenceDescription) ? "" : $"Entry Desc: {x.ReferenceDescription}\n") +
                (string.IsNullOrWhiteSpace(x.ArtUrl) ? "" : $"Art Url: <{x.ArtUrl}>\n")));
            await user.SendMessageAsync(info + entries);
        }

        [Command("clear")]
        [Alias("delete")]
        [Summary("delete all art trade entries")]
        [InfoModule.SummaryDetail("forcefully removes all entries, use with caution" +
            "\nit is possible to undo this operation using the restore command")]
        public async Task ClearAll()
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppData);
            Storage.Axx.ClearStorage(Storage.Axx.AppData);
            await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
        }

        [Command("shuffle")]
        [Summary("randomly shuffle art trade entries")]
        [InfoModule.SummaryDetail("randomly shuffles entered entries generating a chain" +
            "\nthe chain goes only one way, meaning that each entry has a next entry, and the next entry is the first entrie's partner" +
            "\nbut the first entry does not see it's previous entry, so they do not know who has them as their partner" +
            "\nif specified, a direct message is sent to each participant containing their partner's information")]
        public async Task Shuffle([Summary("bool flag indicating if participants should be notified")]bool bNotify = false)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppData);
            Storage.Axx.AppData.Shuffle(Storage.Axx.AppHistory);
            await ReplyAsync(string.Format(Properties.Resources.TRADE_ENTRIES_SHUFFLE, user.Id));
            if (bNotify)
                await SendPartners();
        }
                
        [Command("swap")]
        [Summary("changes your art trade partner")]
        [InfoModule.SummaryDetail("if you input only one participant's name, the bot tries to swap you with the participant" +
            "\nif you enter two participant names, the bot tries to swap those two participants" +
            "\nif you enter three participant names, the bot will try to swap the three participants with minimal impact to other participants" +
            "\nat least three voluntary participants are needed if no other participant should have his partner forcefully changed")]
        public async Task ChangeMyPair([Summary("first partner's id, this can be either his name, nickname or discord user UUID")]string partner1Id
            , [Summary("second partner's id, if not entered, the caller is set as the second partner (optional)")]string partner2Id = null
            , [Summary("third partner's id, needed for non-forceful partner change (optional)")]string partner3Id = null)
        {
            var ourUser = Context.Message.Author;
            if (!Utils.IsAdminUser(ourUser))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, ourUser.Id));
                return;
            }

            var guild = Utils.FindGuild(ourUser);
            if (guild == null)
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, ourUser.Id));
                return;
            }
                        
            SocketUser partner1 = Utils.FindUser(guild, partner1Id);
            if (partner1 == null)
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, ourUser.Id));
                return;
            }

            SocketUser partner2 = null;
            if (!string.IsNullOrWhiteSpace(partner2Id))
            {
                partner2 = Utils.FindUser(guild, partner2Id);
                if (partner2 == null)
                {
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, ourUser.Id));
                    return;
                }
            }
            else
            {
                partner2 = ourUser;
            }

            if (partner1 == partner2)
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, ourUser.Id));
                return;
            }

            ulong? partner3IdVal = null;
            if (!string.IsNullOrWhiteSpace(partner3Id))
            {
                SocketUser partner3 = Utils.FindUser(guild, partner3Id);
                if (partner3 == null || partner3 == partner2 || partner3 == partner1)
                {
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, ourUser.Id));
                    return;
                }
                else
                {
                    partner3IdVal = partner3.Id;
                }
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppData);

            List<Storage.UserData> needNotify;
            if (Storage.Axx.AppData.ResetNext(partner2.Id, partner1.Id, partner3IdVal, out needNotify))
            {
                if (await SendPartnersResponse(needNotify))
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, ourUser.Id));
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, ourUser.Id));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, ourUser.Id));
        }

        [Command("restore")]
        [Summary("restores art trade entries from backup files / embeded JSON file")]
        [InfoModule.SummaryDetail("works as an undo button if either one of the entries, settings or history storage has been edited by a previous command" +
            "\nyou can specify which of the three should be restored in the command parameter, or leave the parameter empty which will restore all three" +
            "\nyou can also add a JSON file by embedding it into the message, the storage type restored this way depends on the input parameter or the JSON file's name")]
        public async Task RestoreAll([Summary("type of storage that should be restored, only `entries`, `settings` or `history` is supported (optional)")][Remainder]string storageType = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            var attachments = Context.Message.Attachments;
            if (attachments.Count <= 0)
            {
                if (string.IsNullOrWhiteSpace(storageType))
                {
                    if (await Storage.Axx.RestoreStorage(Storage.Axx.AppData) && await Storage.Axx.RestoreStorage(Storage.Axx.AppSettings) && await Storage.Axx.RestoreStorage(Storage.Axx.AppHistory))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
                }
                else if (storageType.ToLower().Trim().Contains("entries"))
                {
                    if (await Storage.Axx.RestoreStorage(Storage.Axx.AppData))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
                }
                else if (storageType.ToLower().Trim().Contains("settings"))
                {
                    if (await Storage.Axx.RestoreStorage(Storage.Axx.AppSettings))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
                }
                else if (storageType.ToLower().Trim().Contains("history"))
                {
                    if (await Storage.Axx.RestoreStorage(Storage.Axx.AppHistory))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
                }
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, user.Id));
            }
            else
            {
                string fileUrl = attachments.FirstOrDefault().Url;
                Uri uri = new Uri(fileUrl);
                if (string.IsNullOrWhiteSpace(storageType))
                {
                    string filename = System.IO.Path.GetFileName(uri.LocalPath).Trim().ToLower();
                    if (filename.Contains("storage"))
                        storageType = "entries";
                    else if (filename.Contains("settings"))
                        storageType = "settings";
                    else if (filename.Contains("history"))
                        storageType = "history";
                }

                if (storageType.ToLower().Trim().Contains("entries"))
                {
                    if (await Storage.Axx.RestoreStorage(Storage.Axx.AppData, fileUrl))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
                }
                else if (storageType.ToLower().Trim().Contains("settings"))
                {
                    if (await Storage.Axx.RestoreStorage(Storage.Axx.AppSettings, fileUrl))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
                }
                else if (storageType.ToLower().Trim().Contains("history"))
                {
                    if (await Storage.Axx.RestoreStorage(Storage.Axx.AppHistory, fileUrl))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
                }
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, user.Id));
            }
        }

        [Command("backup")]
        [Summary("sync backup file / flush database in a DM as separate JSON files")]
        [InfoModule.SummaryDetail("creates a checkpoint of the entries, settings and history storages" +
            "\nyou can also specify a bool flag, if you don't want to checkpoint, but want to send the storages as JSON files in a direct message instead" +
            "\ncheckpoints are done automatically if at least one of the storage types has been changed by a storage editing command, so there is usually no need to call this manually")]
        public async Task FlushStorage([Summary("bool flag that indicates if the storages should be sent as JSON files in a DM")]bool bSendJSON = false)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            if (bSendJSON)
            {
                await user.SendFileAsync(Storage.Axx.AppDataFileName);
                await user.SendFileAsync(Storage.Axx.AppSettingsFileName);
                await user.SendFileAsync(Storage.Axx.AppHistoryFileName);
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
            }
            else
            {
                Storage.Axx.BackupStorage(Storage.Axx.AppData);
                Storage.Axx.BackupStorage(Storage.Axx.AppSettings);
                Storage.Axx.BackupStorage(Storage.Axx.AppHistory);
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_DONE, user.Id));
            }
        }

        public async static Task<(string, string, string)> SendPartnersResponseStatic(DiscordSocketClient client, List<Storage.UserData> entries, bool bThemeOnly = false)
        {
            string report1 = "";
            string report2 = "";
            string report3 = "";
            foreach (var userData in entries)
            {
                Storage.UserData nextUser;
                if (!Storage.Axx.AppData.Next(userData.UserId, out nextUser))
                {
                    report1 += $"{userData.UserName}\n";
                    continue;
                }

                Embed embed = null;
                if (!string.IsNullOrWhiteSpace(nextUser.ReferenceUrl))
                    embed = new EmbedBuilder().WithImageUrl(nextUser.ReferenceUrl).Build();

                if (string.IsNullOrWhiteSpace(nextUser.ReferenceDescription) && embed == null)
                {
                    report2 += $"{userData.UserName}\n";
                    continue;
                }

                var socketUser = client.GetUser(userData.UserId);
                if (socketUser == null)
                {
                    report3 += $"{userData.UserName}\n";
                    continue;
                }

                await ReferenceModule.SendPartnerResponse(nextUser, socketUser, bThemeOnly);
            }
            return (report1, report2, report3);
        }

        public async Task<bool> SendPartnersResponse(List<Storage.UserData> entries, bool bThemeOnly = false)
        {
            var report = await SendPartnersResponseStatic(Context.Client, entries, bThemeOnly);

            string finalReport = "";
            if (!string.IsNullOrWhiteSpace(report.Item1))
                finalReport += string.Format(Properties.Resources.TRADE_SEND_PARTNERS_MISSING) + " \n" + report.Item1;

            if (!string.IsNullOrWhiteSpace(report.Item2))
                finalReport += string.Format(Properties.Resources.TRADE_SEND_ENTRIES_MISSING) + " \n" + report.Item2;

            if (!string.IsNullOrWhiteSpace(report.Item3))
                finalReport += string.Format(Properties.Resources.TRADE_SEND_USERS_MISSING) + " \n" + report.Item3;

            if (!string.IsNullOrWhiteSpace(finalReport))
            {
                await Context.Message.Author.SendMessageAsync(finalReport);
                return false;
            }

            return true;
        }

        [Command("resend")]
        [Summary("send to all participants their trade partner's entry in a DM")]
        [InfoModule.SummaryDetail("sends a direct message to all art trade participants containing the entry information of their trade partner" +
            "\nthis is done automatically when the trade month starts, so there is usually no need to call this manually")]
        public async Task SendPartners([Summary("bool flag specifies if only the current theme info should be resent (optional)")]bool bThemeOnly = false)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            if (!await SendPartnersResponse(Storage.Axx.AppData.Storage, bThemeOnly))
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_REQUEST_FAIL, user.Id));
        }
    }
}
