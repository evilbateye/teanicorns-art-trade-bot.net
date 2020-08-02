using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord;
using Discord.WebSocket;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;

namespace teanicorns_art_trade_bot.Modules
{
    //[RequireUserPermissionAttribute(GuildPermission.Administrator)]
    //[Group(Utils.ADMIN_GROUP_ID)]
    public class TradeEventModule : ModuleBase<SocketCommandContext>
    {
        public CommandService CommandService { get; set; }

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
            return Storage.xs.History.GetTrade(level);
        }
        public static List<Storage.UserData> GetMissingArt(int? level = null)
        {
            if (!level.HasValue)
                return GetMissingArt(Storage.xs.Entries);
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
                
        public static async Task<bool> StartEntryWeek(DiscordSocketClient client, double? days2end = null, bool? force = null, [Remainder]string theme = "")
        {
            SocketTextChannel channel = Utils.FindChannel(client, Storage.xs.Settings.GetWorkingChannel());
            if (channel == null)
                return false;

            Storage.xs.History.RecordTrade(Storage.xs.Entries);
            await GoogleDriveHandler.UploadGoogleFile(Storage.xs.HISTORY_PATH);
            Storage.xs.ClearStorage(Storage.xs.Entries);
            Storage.xs.Settings.ActivateTrade(Storage.ApplicationSettings.TradeSegment.EntryWeek, null/*days2start*/, days2end, force, true /*bResetPoll*/);
            Storage.xs.Entries.SetTheme(theme);
                        
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

            string outMessage = $"{string.Format(Properties.Resources.ENTRY_WEEK, Config.CmdPrefix, "set entry", "about")}\n"
                + (string.IsNullOrWhiteSpace(Storage.xs.Entries.GetTheme()) ? "" : string.Format(Properties.Resources.TRADE_THIS_THEME, Storage.xs.Entries.GetTheme()) + "\n");

            string naughtyList = "";
            if (!string.IsNullOrWhiteSpace(artMissing))
                naughtyList += $"\n`{artHistory0.GetTheme()}` : {artMissing}";
            if (!string.IsNullOrWhiteSpace(artMissingHistory1))
                naughtyList += $"\n`{artHistory1.GetTheme()}` : {artMissingHistory1}";
            if (!string.IsNullOrWhiteSpace(artMissingHistory2))
                naughtyList += $"\n`{artHistory2.GetTheme()}` : {artMissingHistory2}";
            if (!string.IsNullOrWhiteSpace(naughtyList))
                outMessage += $"**Naughty List**{naughtyList}\n{string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, $"reveal art <theme name>", "to register the missing art for that specific theme")}";

            await channel.SendMessageAsync(outMessage, embed: Utils.EmbedFooter(client));

            await Utils.CreateThemePoll(client);

            var subscribers = new List<ulong>(Storage.xs.Settings.GetSubscribers());

            // notify those that did not send their art on time
            foreach (Storage.UserData user in GetMissingArt(artHistory0))
            {
                subscribers.Remove(user.UserId); // don't notify subscribers twice

                SocketUser su = client.GetUser(user.UserId);
                if (su != null)
                    await su.SendMessageAsync(string.Format(Properties.Resources.TRADE_ART_LATE_DM, user.UserId, artHistory0.GetTheme())
                        + $"\n{string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, $"reveal art {artHistory0.GetTheme()}", "to register the missing art")}", embed: Utils.EmbedFooter(client));
            }

            // notify those that subscribed for notifications
            await Utils.NotifySubscribers(client, string.Format(Properties.Resources.ENTRY_WEEK, Config.CmdPrefix, "set entry", "about"), subscribers);
            return true;
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
            , [Summary("theme that will be set for the next art trade (optional)")][Remainder]string theme = "")
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            if (Storage.xs.Settings.IsTradeMonthActive())
            {
                Storage.xs.BackupStorage(Storage.xs.Entries);
                Storage.xs.BackupStorage(Storage.xs.Settings);
                Storage.xs.BackupStorage(Storage.xs.History);

                await StartEntryWeek(Context.Client, days2end, force, theme);
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_IN_PROGRESS, user.Id, "entry week"));
        }
                        
        public static async Task<bool> StartTradeMonth(DiscordSocketClient client, double? days2end = null, bool? force = null)
        {
            SocketTextChannel channel = Utils.FindChannel(client, Storage.xs.Settings.GetWorkingChannel());
            if (channel == null)
                return false;
            
            Storage.xs.Settings.ActivateTrade(Storage.ApplicationSettings.TradeSegment.TradeMonth, 0.0/*days2start*/, days2end, force, true /*bResetPoll*/);

            Storage.xs.Entries.DoShuffle(Storage.xs.History);

            await channel.SendMessageAsync($"{string.Format(Properties.Resources.TRADE_MONTH, Config.CmdPrefix, "reveal art", "about")}\n"
            + (string.IsNullOrWhiteSpace(Storage.xs.Entries.GetTheme()) ? "" : string.Format(Properties.Resources.TRADE_THIS_THEME, Storage.xs.Entries.GetTheme()) + "\n")
            + (Storage.xs.Settings.GetTradeDays() == 0 ? "" : string.Format(Properties.Resources.TRADE_ENDS_ON, Storage.xs.Settings.GetTradeDays(), Storage.xs.Settings.GetTradeStart(Storage.xs.Settings.GetTradeDays()).ToString("dd-MMMM")))
            , embed: Utils.EmbedFooter(client));

            return await SendPartnersResponse(client);
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
            , [Summary("theme that will be set for the next art trade (optional)")][Remainder]string theme = "")
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            Storage.xs.BackupStorage(Storage.xs.Entries);
            Storage.xs.BackupStorage(Storage.xs.Settings);

            if (!Storage.xs.Settings.IsTradeMonthActive())
            {
                if (!string.IsNullOrWhiteSpace(theme))
                    Storage.xs.Entries.SetTheme(theme);

                if (string.IsNullOrWhiteSpace(Storage.xs.Entries.GetTheme()))
                    Storage.xs.Entries.SetTheme(await Utils.GetThemePollResult(Context.Client));

                if (!await StartTradeMonth(Context.Client, days2end, force))
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to start trade month"));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_IN_PROGRESS, user.Id, "trade month"));
        }

        [Command("theme")]
        [Alias("th")]
        [Summary("set the art trade theme")]
        public async Task Theme([Summary("theme to be set for the next art trade")][Remainder]string theme)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            if (string.IsNullOrWhiteSpace(theme))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, user.Id));
                return;
            }

            Storage.xs.BackupStorage(Storage.xs.Entries);

            if (Storage.xs.Entries.SetTheme(theme))
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "trade theme has been set"));
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to set theme"));
        }

        [Command("settings")]
        [Summary("silently changes the current settings")]
        [InfoModule.SummaryDetail("you can silently turn the art trade on/off" +
            "\nchange the start date of the trade and number of days until the trade ends" +
            "\nyou can also modify the force flag, or change the active trading channel")]
        public async Task Settings([Summary("bool flag indicating trade start/end")]int? tradeSeg = null
            , [Summary("number of days until the next trade starts (the duration of the entry week) (optional)")]double? days2start = null
            , [Summary("number of days until the next trade ends (the duration of the trade month) (optional)")]double? days2end = null
            , [Summary("bool flag indicating if the next trade should be forced to end automatically at the end (optional)")] bool? force = null
            , [Summary("bool flag indicating if theme poll should be reset (optional)")] bool bResetPoll = false
            , [Summary("name of the only channel where the art trade bot listens for user input")][Remainder]string channel = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            Storage.xs.BackupStorage(Storage.xs.Settings);
            Storage.xs.Settings.ActivateTrade((Storage.ApplicationSettings.TradeSegment?)tradeSeg, days2start, days2end, force, bResetPoll);

            if (!string.IsNullOrWhiteSpace(channel))
                await Channel(channel);
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "settings have been overriden"));
        }

        [Command("channel")]
        [Summary("sets the working channel")]
        [InfoModule.SummaryDetail("the art trade bot listens for user input messages only in this channel")]
        public async Task Channel([Summary("name of the channel")][Remainder]string channel)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            channel = channel.ToLower().Trim();
            if (string.IsNullOrWhiteSpace(channel))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, user.Id));
                return;
            }

            Storage.xs.BackupStorage(Storage.xs.Settings);
            Storage.xs.Settings.SetThemePollID(0);

            var channelObj = Utils.FindChannel(Utils.FindGuild(user), channel);
            if (channelObj != null && Storage.xs.Settings.SetWorkingChannel(channel))
            {
                if (await Utils.CreateHelp(Context.Client, CommandService, channel))
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "channel has been set"));
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to create help message"));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to set working channel"));
        }

        [Command("list")]
        [Alias("ls")]
        [Summary("sends you a list of all entries in a DM")]
        [InfoModule.SummaryDetail("sends detailed information about art trade entries" +
            "\nthe information is sent using a direct message, because it contains secrets that should not be visible to other trade participants")]
        public async Task List([Summary("bool flag indicating if a more detailed info should be shown")]bool bAll = false)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }
                        
            string info = string.Format(Properties.Resources.TRADE_LIST_ONOFF, Storage.xs.Settings.GetActiveTradeSegment()) + "\n";
            
            info += string.Format(Properties.Resources.TRADE_THIS_THEME, string.IsNullOrWhiteSpace(Storage.xs.Entries.GetTheme()) ? "none" : Storage.xs.Entries.GetTheme()) + "\n";
                        
            info += string.Format(Properties.Resources.TRADE_LIST_SETTINGS
                , Storage.xs.Settings.GetWorkingChannel()
                , Storage.xs.Settings.GetTradeStart().ToString("dd-MMMM")
                , Storage.xs.Settings.GetTradeEnd().ToString("dd-MMMM")
                , Storage.xs.Settings.GetTradeDays()
                , Storage.xs.Settings.GetNotifyFlags()
                , Storage.xs.Settings.IsForceTradeOn() + "\n");

            info += "registered messages: " + (Storage.xs.Settings.GetMsgIDs().Count() <= 0 ? "`empty`" : string.Join(", ", Storage.xs.Settings.GetMsgIDs().Select(x => $"`{x}`"))) + "\n";
            info += "subscribers: " + (Storage.xs.Settings.GetSubscribers().Count <= 0 ? "`empty`" : string.Join(", ", Storage.xs.Settings.GetSubscribers().Select(sub => $"`{Context.Client.GetUser(sub).Username}`"))) + "\n";
            info += "theme pool: " + (Storage.xs.Settings.GetThemePool().Count <= 0 ? "`empty`" : string.Join(",", Storage.xs.Settings.GetThemePool().Select(pair => $"`{Context.Client.GetUser(pair.Key).Username}` ({string.Join(", ", pair.Value.Select(theme => $"`{theme}`"))})"))) + "\n";

            string entries = $"\n**({Storage.xs.Entries.Count()})** **Entries**\n";
            if (!bAll)
                entries += string.Join("\n", Storage.xs.Entries.GetStorage().Select(x => $"`{x.UserName}`"
                + (string.IsNullOrWhiteSpace(x.NickName) ? "" : $" (`{x.NickName}`)")
                + (string.IsNullOrWhiteSpace(x.ArtUrl) ? "" : " , `art`")
                ));
            else
                entries += string.Join("\n", Storage.xs.Entries.GetStorage().Select(x => $"`{x.UserName}`: `{x.UserId}`\n" +
                (string.IsNullOrWhiteSpace(x.NickName) ? "" : $"Nickname: `{x.NickName}`\n")
                + (string.IsNullOrWhiteSpace(x.ReferenceUrl) ? "" : $"Entry Url: <{x.ReferenceUrl}>\n")
                + (string.IsNullOrWhiteSpace(x.ReferenceDescription) ? "" : $"Entry Desc: `{x.ReferenceDescription}`\n")
                + (string.IsNullOrWhiteSpace(x.ArtUrl) ? "" : $"Art Url: <{x.ArtUrl}>\n")
                ));
            await user.SendMessageAsync(info + entries);
        }

        [Command("clear")]
        [Alias("delete")]
        [Summary("delete all art trade entries")]
        [InfoModule.SummaryDetail("forcefully removes all entries, use with caution" +
            "\nit is possible to undo this operation using the restore command")]
        public async Task Clear()
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            Storage.xs.BackupStorage(Storage.xs.Entries);
            Storage.xs.ClearStorage(Storage.xs.Entries);
            await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "entries have been cleared"));
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
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            Storage.xs.BackupStorage(Storage.xs.Entries);
            Storage.xs.Entries.DoShuffle(Storage.xs.History);
            await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "I shuffled all entries"));
            if (bNotify)
                await Resend();
        }
                
        [Command("swap")]
        [Summary("changes your art trade partner")]
        [InfoModule.SummaryDetail("if you input only one participant's name, the bot tries to swap you with the participant" +
            "\nif you enter two participant names, the bot tries to swap those two participants" +
            "\nif you enter three participant names, the bot will try to swap the three participants with minimal impact to other participants" +
            "\nat least three voluntary participants are needed if no other participant should have his partner forcefully changed")]
        public async Task Swap([Summary("first partner's id, this can be either his name, nickname or discord user UUID")]string partner1Id
            , [Summary("second partner's id, if not entered, the caller is set as the second partner (optional)")]string partner2Id = null
            , [Summary("third partner's id, needed for non-forceful partner change (optional)")]string partner3Id = null)
        {
            var ourUser = Context.Message.Author;
            if (!Utils.IsAdminUser(ourUser))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, ourUser.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            var guild = Utils.FindGuild(ourUser);
            if (guild == null)
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, ourUser.Id, "guild user not found"));
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

            Storage.xs.BackupStorage(Storage.xs.Entries);

            List<Storage.UserData> needNotify;
            if (Storage.xs.Entries.ResetNext(partner2.Id, partner1.Id, partner3IdVal, out needNotify))
            {
                if (await SendPartnersResponse(Context.Client, needNotify))
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, ourUser.Id, "partners have been swapped"));
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, ourUser.Id, "unable to notify partners related to the swap"));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, ourUser.Id, "unable to swap partners"));
        }

        [Command("restore")]
        [Summary("restores art trade entries from backup files / embeded JSON file")]
        [InfoModule.SummaryDetail("works as an undo button if either one of the entries, settings or history storage has been edited by a previous command" +
            "\nyou can specify which of the three should be restored in the command parameter, or leave the parameter empty which will restore all three" +
            "\nyou can also add a JSON file by embedding it into the message, the storage type restored this way depends on the input parameter or the JSON file's name")]
        public async Task Restore([Summary("type of storage that should be restored, only `entries`, `settings` or `history` is supported (optional)")][Remainder]string storageType = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            var attachments = Context.Message.Attachments;
            if (attachments.Count <= 0)
            {
                if (string.IsNullOrWhiteSpace(storageType))
                {
                    if (await Storage.xs.RestoreStorage(Storage.xs.Entries) && await Storage.xs.RestoreStorage(Storage.xs.Settings) && await Storage.xs.RestoreStorage(Storage.xs.History))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "all storages have been restored from disk"));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to restore all storages from disk"));
                }
                else if (storageType.ToLower().Trim().Contains("entries"))
                {
                    if (await Storage.xs.RestoreStorage(Storage.xs.Entries))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "entries storage has been restored from disk"));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to restore entries storage from disk"));
                }
                else if (storageType.ToLower().Trim().Contains("settings"))
                {
                    if (await Storage.xs.RestoreStorage(Storage.xs.Settings))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "settings storage has been restored from disk"));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to restore settings storage from disk"));
                }
                else if (storageType.ToLower().Trim().Contains("history"))
                {
                    if (await Storage.xs.RestoreStorage(Storage.xs.History))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "history storage has been restored from disk"));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to restore history storage from disk"));
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
                    if (await Storage.xs.RestoreStorage(Storage.xs.Entries, fileUrl))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "entries storage has been restored from cloud"));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to restore entries storage from cloud"));
                }
                else if (storageType.ToLower().Trim().Contains("settings"))
                {
                    if (await Storage.xs.RestoreStorage(Storage.xs.Settings, fileUrl))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "settings storage has been restored from cloud"));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to restore settings storage from cloud"));
                }
                else if (storageType.ToLower().Trim().Contains("history"))
                {
                    if (await Storage.xs.RestoreStorage(Storage.xs.History, fileUrl))
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "history storage has been restored from cloud"));
                    else
                        await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to restore history storage from cloud"));
                }
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, user.Id));
            }
        }

        [Command("backup")]
        [Summary("sync backup file / flush database in a DM as separate JSON files")]
        [InfoModule.SummaryDetail("creates a checkpoint of the entries, settings and history storages" +
            "\ncheckpoints are done automatically if at least one of the storage types has been changed by a storage editing command, so there is usually no need to call this manually" +
            "\nyou can specify if you want to send the storages as JSON files in a direct message to you instead" +
            "\nyou can also specify if you want to sync the the storage types with google drive instead")]
        public async Task Backup([Summary("string mode empty = checkpoint, json = send json files in DM, sync = sync to gDrive")]string mode = "")
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            mode = mode.ToLower().Trim();
            if (mode.Equals("json"))
            {
                await user.SendFileAsync(Storage.xs.ENTRIES_PATH);
                await user.SendFileAsync(Storage.xs.HISTORY_PATH);
                await user.SendFileAsync(Storage.xs.SETTINGS_PATH);
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "all storages have been dumped"));
            }
            else if (mode.Equals("sync"))
            {
                await GoogleDriveHandler.UploadGoogleFile(Storage.xs.ENTRIES_PATH);
                await GoogleDriveHandler.UploadGoogleFile(Storage.xs.HISTORY_PATH);
                await GoogleDriveHandler.UploadGoogleFile(Storage.xs.SETTINGS_PATH);
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "all storages have been synced with cloud"));
            }
            else
            {
                Storage.xs.BackupStorage(Storage.xs.Entries);
                Storage.xs.BackupStorage(Storage.xs.Settings);
                Storage.xs.BackupStorage(Storage.xs.History);
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "all storages have been backed up on disk"));
            }
        }

        public static async Task<bool> SendPartnersResponse(DiscordSocketClient client, List<Storage.UserData> entries = null, bool bThemeOnly = false)
        {
            if (entries == null)
                entries = Storage.xs.Entries.GetStorage();

            string report1 = "";
            string report2 = "";
            string report3 = "";
            foreach (var userData in entries)
            {
                Storage.UserData nextUser;
                if (!Storage.xs.Entries.Next(userData.UserId, out nextUser))
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

                await ReferenceModule.SendPartnerResponse(client, nextUser, socketUser, bThemeOnly);
            }

            if (!string.IsNullOrWhiteSpace(report1))
                return false;

            if (!string.IsNullOrWhiteSpace(report2))
                return false;

            if (!string.IsNullOrWhiteSpace(report3))
                return false;

            return true;
        }

        [Command("resend")]
        [Summary("send to all participants their trade partner's entry in a DM")]
        [InfoModule.SummaryDetail("sends a direct message to all art trade participants containing the entry information of their trade partner" +
            "\nthis is done automatically when the trade month starts, so there is usually no need to call this manually")]
        public async Task Resend([Summary("bool flag specifies if only the current theme info should be resent (optional)")]bool bThemeOnly = false)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            if (!await SendPartnersResponse(Context.Client, Storage.xs.Entries.GetStorage(), bThemeOnly))
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to notify trade partners"));
        }

        [Command("create help")]
        [Summary("creates and registers help message")]
        [InfoModule.SummaryDetail("send help message into working channel and register message id")]
        public async Task CreateHelp([Summary("channel where the help message should be sent (optional)")][Remainder]string channelName = "")
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "admin only command"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }
                        
            Storage.xs.BackupStorage(Storage.xs.Settings);

            if (await Utils.CreateHelp(Context.Client, CommandService, channelName))
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "help message has been created"));
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to create help message"));
        }
    }
}
