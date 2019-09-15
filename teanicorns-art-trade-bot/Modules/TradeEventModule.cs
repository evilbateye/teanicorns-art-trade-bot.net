using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord;
using Discord.WebSocket;

namespace teanicorns_art_trade_bot.Modules
{
    //[RequireUserPermissionAttribute(GuildPermission.Administrator)]
    //[Group(Utils.adminGroupId)]
    public class TradeEventModule : ModuleBase<SocketCommandContext>
    {
        public static string GetMissingArt()
        {
            string artMissing = "";
            foreach (Storage.UserData x in Storage.Axx.AppData.GetStorage())
            {
                if (string.IsNullOrWhiteSpace(x.ArtUrl))
                {
                    artMissing += (string.IsNullOrWhiteSpace(x.NickName) ? x.UserName : x.NickName) + ", ";
                }
            }
            return artMissing;
        }
        public static async Task StartEntryWeek(ISocketMessageChannel channel, uint? days = null, bool? force = null, [Remainder]string theme = null)
        {
            string artMissing = GetMissingArt();
            Storage.Axx.AppHistory.RecordTrade(Storage.Axx.AppData);
            await GoogleDriveHandler.UploadGoogleFile(Storage.Axx.AppHistoryFileName);
            Storage.Axx.ClearStorage(Storage.Axx.AppData);
            Storage.Axx.AppSettings.ActivateTrade(false, days, force);
            if (string.IsNullOrWhiteSpace(theme))
                Storage.Axx.AppData.SetTheme("");
            else
                Storage.Axx.AppData.SetTheme(theme);

            await channel.SendMessageAsync(string.Format(Properties.Resources.TRADE_NEW_ENTRIES) + "\n"
                + (string.IsNullOrWhiteSpace(Storage.Axx.AppData.Theme) ? "" : string.Format(Properties.Resources.TRADE_THIS_THEME, Storage.Axx.AppData.Theme) + "\n")
                + (Storage.Axx.AppSettings.TradeDays == 0 ? "" : string.Format(Properties.Resources.TRADE_ENDS_ON, Storage.Axx.AppSettings.TradeStart.AddDays(Storage.Axx.AppSettings.TradeDays).ToString("dd-MMMM")) + "\n")
                + (string.IsNullOrWhiteSpace(artMissing) ? string.Format(Properties.Resources.TRADE_ART_ON_TIME) : string.Format(Properties.Resources.TRADE_ART_LATE, artMissing)));
        }

        [Command("entry week")]
        [Alias("ew")]
        [Summary("Stops the art trade, clears all entries and theme, starts accepting entries.")]
        public async Task EntryWeek(uint? days = null, bool? force = null, [Remainder]string theme = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppData);
            Storage.Axx.BackupStorage(Storage.Axx.AppSettings);

            if (Storage.Axx.AppSettings.ArtTradeActive != false)
            {
                await StartEntryWeek(Context.Channel, days, force, theme);
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.TRADE_EW_IN_PROGRESS, user.Id));
        }

        [Command("trade month")]
        [Alias("tm")]
        [Summary("Starts the art trade, shuffles entries, sends all partners in a DM, stops accepting entries.")]
        public async Task TradeMonth(uint? days = null, bool? force = null, [Remainder]string theme = null)
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
                Storage.Axx.AppSettings.ActivateTrade(true, days, force);
                if (!string.IsNullOrWhiteSpace(theme))
                    Storage.Axx.AppData.SetTheme(theme);
                Storage.Axx.AppData.Shuffle();

                await ReplyAsync(string.Format(Properties.Resources.TRADE_NO_NEW_ENTRIES) + "\n"
                    + (string.IsNullOrWhiteSpace(Storage.Axx.AppData.Theme) ? "" : string.Format(Properties.Resources.REF_TRADE_THEME, Storage.Axx.AppData.Theme) + "\n")
                    + (Storage.Axx.AppSettings.TradeDays == 0 ? "" : string.Format(Properties.Resources.TRADE_ENDS_ON, Storage.Axx.AppSettings.TradeStart.AddDays(Storage.Axx.AppSettings.TradeDays).ToString("dd-MMMM"))));
                await SendPartners();
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ART_IN_PROGRESS, user.Id));
        }

        [Command("theme")]
        [Alias("th")]
        [Summary("Set the art trade theme.")]
        public async Task SetTheme([Remainder]string theme)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            if (string.IsNullOrWhiteSpace(theme))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_THEME_NULL, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppData);

            if (Storage.Axx.AppData.SetTheme(theme))
                await ReplyAsync(string.Format(Properties.Resources.TRADE_THEME_SET, user.Id));
            else
                await ReplyAsync(string.Format(Properties.Resources.TRADE_THEME_PROBLEM, user.Id));
        }

        [Command("start trade")]
        [Alias("st")]
        [Summary("Turns on/off the art trade (silent), sets start date to now, sets number of days until end.")]
        public async Task StartTrade(bool bStart, uint? days = null, bool? force = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppSettings);
            Storage.Axx.AppSettings.ActivateTrade(bStart, days, force);
            await ReplyAsync(string.Format(Properties.Resources.TRADE_ACTIVE_SET, user.Id));
        }

        [Command("channel")]
        [Alias("ch")]
        [Summary("Sets the working channel for ATB.")]
        public async Task WorkChannel(string channel)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppSettings);

            if (Storage.Axx.AppSettings.SetWorkingChannel(channel))
                await ReplyAsync(string.Format(Properties.Resources.TRADE_CHANNEL_SET, user.Id));
            else
                await ReplyAsync(string.Format(Properties.Resources.TRADE_CHANNEL_PROBLEM, user.Id, Storage.Axx.AppSettings.WorkingChannel));
        }

        [Command("list")]
        [Alias("l")]
        [Summary("Sends you a list of all entries in a DM.")]
        public async Task ListAllEntries(string all = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            string info = (Storage.Axx.AppSettings.ArtTradeActive ? string.Format(Properties.Resources.TRADE_TAKING_PLACE_TM) : string.Format(Properties.Resources.TRADE_TAKING_PLACE_EW)) + "\n";

            if (!string.IsNullOrWhiteSpace(Storage.Axx.AppData.Theme))
                info += string.Format(Properties.Resources.TRADE_THIS_THEME, Storage.Axx.AppData.Theme) + "\n";

            info += string.Format(Properties.Resources.TRADE_LIST_OTHER_INFO
                , Storage.Axx.AppSettings.WorkingChannel
                , Storage.Axx.AppSettings.TradeStart.ToString("dd-MMMM")
                , Storage.Axx.AppSettings.GetTradeEnd().ToString("dd-MMMM")
                , Storage.Axx.AppSettings.TradeDays
                , Storage.Axx.AppSettings.Notified
                , Storage.Axx.AppSettings.ForceTradeEnd) + "\n";

            string entries = string.Format(Properties.Resources.TRADE_LISTING_ALL, user.Id) + "\n";
            if (string.IsNullOrWhiteSpace(all) || all != "all")
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
        [Alias("cl")]
        [Summary("Delete all art trade entries.")]
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
            await ReplyAsync(string.Format(Properties.Resources.TRADE_ENTRIES_CLEARED, user.Id));
        }

        [Command("shuffle")]
        [Alias("sf")]
        [Summary("Randomly shuffle art trade entries.")]
        public async Task Shuffle()
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppData);
            Storage.Axx.AppData.Shuffle();
            await ReplyAsync(string.Format(Properties.Resources.TRADE_ENTRIES_SHUFFLE, user.Id));
        }
                
        [Command("swap")]
        [Alias("sw")]
        [Summary("Changes your art trade partner.")]
        public async Task ChangeMyPair(string partner1Name, string partner2Name = null)
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

            SocketUser partner1 = Utils.FindUser(guild, partner1Name);
            if (partner1 == null)
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_CHANGE_PAIR_MISSING_FIRST, ourUser.Id));
                return;
            }

            SocketUser partner2 = null;
            if (partner2Name != null)
            {
                partner2 = Utils.FindUser(guild, partner2Name);
                if (partner2 == null)
                {
                    await ReplyAsync(string.Format(Properties.Resources.TRADE_CHANGE_PAIR_MISSING_SECOND, ourUser.Id));
                    return;
                }
            }
            else
            {
                partner2 = ourUser;
            }

            Storage.Axx.BackupStorage(Storage.Axx.AppData);

            if (Storage.Axx.AppData.ResetNext(partner2.Id, partner1.Id))
            {
                await ourUser.SendMessageAsync(string.Format(Properties.Resources.TRADE_CHANGE_PAIR_DONE, ourUser.Id, Format.Bold(partner2.Username), Format.Bold(partner1.Username)));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.TRADE_CHANGE_PAIR_PROBLEM, ourUser.Id));
        }

        [Command("restore")]
        [Alias("rs")]
        [Summary("Restores art trade entries from backup file / embeded JSON file.")]
        public async Task RestoreAll()
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
                if (await Storage.Axx.RestoreStorage(Storage.Axx.AppData) && await Storage.Axx.RestoreStorage(Storage.Axx.AppSettings))
                    await ReplyAsync(string.Format(Properties.Resources.TRADE_RESTORE_DONE, user.Id));
                else
                    await ReplyAsync(string.Format(Properties.Resources.TRADE_RESTORE_NO_BACKUP, user.Id));
            }
            else
            {
                string fileUrl = attachments.FirstOrDefault().Url;
                if (await Storage.Axx.RestoreStorage(Storage.Axx.AppData, fileUrl) && await Storage.Axx.RestoreStorage(Storage.Axx.AppSettings, fileUrl))
                    await ReplyAsync(string.Format(Properties.Resources.TRADE_RESTORE_DATABASE_DONE, user.Id));
                else
                    await ReplyAsync(string.Format(Properties.Resources.TRADE_RESTORE_DATABASE_PROBLEM, user.Id));
            }
        }

        [Command("backup")]
        [Alias("bp")]
        [Summary("Update backup file / flush entire ATB database in a DM as a JSON file.")]
        public async Task FlushStorage(string json = "")
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            if (string.IsNullOrWhiteSpace(json) || json != "json")
            {
                Storage.Axx.BackupStorage(Storage.Axx.AppData);
                Storage.Axx.BackupStorage(Storage.Axx.AppSettings);
                await ReplyAsync(string.Format(Properties.Resources.TRADE_BACKUP_DONE, user.Id));
            }
            else
                await user.SendFileAsync(Storage.Axx.AppDataFileName, string.Format(Properties.Resources.TRADE_BACKUP_DATABASE_DONE, user.Id));
        }

        [Command("history")]
        [Alias("hi")]
        [Summary("Show entire ATB database history in a DM as a JSON file.")]
        public async Task FlushHistory()
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
                await user.SendFileAsync(Storage.Axx.AppHistoryFileName, string.Format(Properties.Resources.TRADE_BACKUP_HISTORY_DONE, user.Id));
            }
            else
            {
                string fileUrl = attachments.FirstOrDefault().Url;
                if (await Storage.Axx.RestoreStorage(Storage.Axx.AppHistory, fileUrl))
                    await ReplyAsync(string.Format(Properties.Resources.TRADE_RESTORE_HISTORY_DONE, user.Id));
                else
                    await ReplyAsync(string.Format(Properties.Resources.TRADE_RESTORE_HISTORY_PROBLEM, user.Id));
            }
        }

        [Command("send partners")]
        [Alias("sps")]
        [Summary("Send to all participants their trade partner's entry in a DM.")]
        public async Task SendPartners()
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync(string.Format(Properties.Resources.TRADE_ADMIN_BLOCK, user.Id));
                return;
            }

            string report1 = "";
            string report2 = "";
            string report3 = "";

            var client = Context.Client;
            foreach (var userData in Storage.Axx.AppData.Storage)
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

                await ReferenceModule.SendPartnerResponse(nextUser, socketUser);
            }

            string report = "";
            if (!string.IsNullOrWhiteSpace(report1))
                report += string.Format(Properties.Resources.TRADE_SEND_PARTNERS_MISSING) + " \n" + report1;
            if (!string.IsNullOrWhiteSpace(report2))
                report += string.Format(Properties.Resources.TRADE_SEND_ENTRIES_MISSING) + " \n" + report2;
            if (!string.IsNullOrWhiteSpace(report3))
                report += string.Format(Properties.Resources.TRADE_SEND_USERS_MISSING) + " \n" + report3;

            if (string.IsNullOrWhiteSpace(report))
                report = string.Format(Properties.Resources.TRADE_SEND_PARTNERS_DONE);

            await user.SendMessageAsync(report);
        }
    }
}
