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
        [Command("entry week")]
        [Alias("ew")]
        [Summary("Stops the art trade, clears all entries and theme, starts accepting entries.")]
        public async Task EntryWeek([Remainder]string theme = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            PersistentStorage.BackupStorage();

            if (PersistentStorage.ActivateTrade(false))
            {
                PersistentStorage.ClearStorage();

                if (string.IsNullOrWhiteSpace(theme))
                    PersistentStorage.SetTheme("");
                else
                    PersistentStorage.SetTheme(theme);

                await ReplyAsync($"@everyone the {Format.Bold("entry week")} started. {Format.Bold("We are accepting new entries!")}\n"
                    + (string.IsNullOrWhiteSpace(PersistentStorage.AppData.Theme) ? "" : $" Theme of this art trade is.. \"{PersistentStorage.AppData.Theme}\"."));
            }
            else
                await ReplyAsync($"<@{Context.Message.Author.Id}> the entry week is already in progress.");
        }

        [Command("trade month")]
        [Alias("tm")]
        [Summary("Starts the art trade, shuffles entries, sends all partners in a DM, stops accepting entries.")]
        public async Task TradeMonth([Remainder]string theme = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            PersistentStorage.BackupStorage();

            if (PersistentStorage.ActivateTrade(true))
            {
                if (!string.IsNullOrWhiteSpace(theme))
                    PersistentStorage.SetTheme(theme);

                PersistentStorage.Shuffle();
                await ReplyAsync($"@everyone the art {Format.Bold("trade month")} started. {Format.Bold("We are no longer accepting new entries!")}\n"
                    + (string.IsNullOrWhiteSpace(PersistentStorage.AppData.Theme) ? "" : $" Theme of this art trade is.. \"{PersistentStorage.AppData.Theme}\"."));
                await SendPartners();
            }
            else
                await ReplyAsync($"<@{Context.Message.Author.Id}> the art trade month is already in progress.");
        }

        [Command("theme")]
        [Alias("th")]
        [Summary("Set the art trade theme.")]
        public async Task SetTheme([Remainder]string theme)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            if (string.IsNullOrWhiteSpace(theme))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. The provided theme is null or whitespace.");
                return;
            }

            PersistentStorage.BackupStorage();

            if (PersistentStorage.SetTheme(theme))
            {
                await ReplyAsync($"The theme has been set successfully <@{Context.Message.Author.Id}>!");
            }
            else
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. There has been a problem when setting the theme.");
        }

        [Command("channel")]
        [Alias("ch")]
        [Summary("Sets the working channel for ATB.")]
        public async Task WorkChannel(string channel)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            PersistentStorage.BackupStorage();

            if (PersistentStorage.SetWorkingChannel(channel))
            {
                await ReplyAsync($"Channel has been set successfully <@{Context.Message.Author.Id}>.");
            }
            else
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. Was not able to change the working channel. Current working channel is {PersistentStorage.AppData.WorkingChannel}.");
        }

        [Command("list")]
        [Alias("l")]
        [Summary("Sends you a list of all entries in a DM.")]
        public async Task ListAllEntries(string all = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            string info = "Currently taking place.. ";
            if (PersistentStorage.AppData.ArtTradeActive)
                info += $"{Format.Bold("Trade month")}.\n";
            else
                info += $"{Format.Bold("Entry week")}.\n";

            if (!string.IsNullOrWhiteSpace(PersistentStorage.AppData.Theme))
                info += $"This month's theme is.. \"{PersistentStorage.AppData.Theme}\".\n";

            string entries = $"Listing all entries <@{user.Id}>. Each next entry is the partner of the previous one.\n";
            if (string.IsNullOrWhiteSpace(all) || all != "all")
                entries += string.Join("\n", PersistentStorage.GetStorage().Select(x => $"{x.UserName}" +
                (string.IsNullOrWhiteSpace(x.NickName) ? "" : $" ({x.NickName})")));
            else
                entries += string.Join("\n", PersistentStorage.GetStorage().Select(x => $"{x.UserName}\n{x.UserId}\n" +
                (string.IsNullOrWhiteSpace(x.NickName) ? "" : $"({x.NickName})\n") +
                (string.IsNullOrWhiteSpace(x.ReferenceUrl) ? "" : $"<{x.ReferenceUrl}>\n") +
                (string.IsNullOrWhiteSpace(x.ReferenceDescription) ? "" : $"{x.ReferenceDescription}\n")));
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
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            PersistentStorage.BackupStorage();
            PersistentStorage.ClearStorage();
            await ReplyAsync($"All entries successfully removed <@{user.Id}>. Backup file updated.");
        }

        [Command("shuffle")]
        [Alias("sf")]
        [Summary("Randomly shuffle art trade entries.")]
        public async Task Shuffle()
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            PersistentStorage.BackupStorage();
            PersistentStorage.Shuffle();
            await ReplyAsync($"Entries have been shuffled successfully <@{user.Id}>.");
        }
                
        [Command("swap")]
        [Alias("sw")]
        [Summary("Changes your art trade partner.")]
        public async Task ChangeMyPair(string partner1Name, string partner2Name = null)
        {
            var ourUser = Context.Message.Author;
            if (!Utils.IsAdminUser(ourUser))
            {
                await ReplyAsync($"Sorry <@{ourUser.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            var guild = Utils.FindGuild(ourUser);
            if (guild == null)
            {
                await ReplyAsync($"Sorry <@{ourUser.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            SocketUser partner1 = Utils.FindUser(guild, partner1Name);
            if (partner1 == null)
            {
                await ReplyAsync($"Sorry <@{ourUser.Id}>. Could not find the first partner.");
                return;
            }

            SocketUser partner2 = null;
            if (partner2Name != null)
            {
                partner2 = Utils.FindUser(guild, partner2Name);
                if (partner2 == null)
                {
                    await ReplyAsync($"Sorry <@{ourUser.Id}>. Could not find the second partner.");
                    return;
                }
            }
            else
            {
                partner2 = ourUser;
            }

            PersistentStorage.BackupStorage();

            if (PersistentStorage.ResetNext(partner2.Id, partner1.Id))
            {
                await ourUser.SendMessageAsync($"Art trade partner of {Format.Bold(partner2.Username)} has been changed to {Format.Bold(partner1.Username)} <@{ourUser.Id}>.");
            }
            else
                await ReplyAsync($"Could not change your art trade partner.");
        }

        [Command("restore")]
        [Alias("rs")]
        [Summary("Restores art trade entries from backup file / embeded JSON file.")]
        public async Task RestoreAll()
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            var attachments = Context.Message.Attachments;
            if (attachments.Count <= 0)
            {
                if (PersistentStorage.RestoreStorage())
                    await ReplyAsync($"All entries successfully restored <@{user.Id}>.");
                else
                    await ReplyAsync($"Sorry <@{user.Id}>. No backup file found.");
            }
            else
            {
                string fileUrl = attachments.FirstOrDefault().Url;
                if (await PersistentStorage.RestoreStorageFromUrl(fileUrl))
                    await ReplyAsync($"The database has been loaded successfully <@{user.Id}>.");
                else
                    await ReplyAsync($"Sorry <@{user.Id}>. There have been some problems when loading the databse.");
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
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            if (string.IsNullOrWhiteSpace(json) || json != "json")
            {
                PersistentStorage.BackupStorage();
                await ReplyAsync($"Backup file has been updated successfully <@{Context.Message.Author.Id}>.");
            }
            else
                await user.SendFileAsync(PersistentStorage.storageFileName, $"Sending the full database <@{Context.Message.Author.Id}>.");
        }

        [Command("send partners")]
        [Alias("sps")]
        [Summary("Send to all participants their trade partner's entry in a DM.")]
        public async Task SendPartners()
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            string report1 = "";
            string report2 = "";
            string report3 = "";

            var client = Context.Client;
            foreach (var userData in PersistentStorage.AppData.Storage)
            {
                PersistentStorage.UserData nextUser;
                if (!PersistentStorage.Next(userData.UserId, out nextUser))
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
                report += "Could not find an art trade partner for: \n" + report1;
            if (!string.IsNullOrWhiteSpace(report2))
                report += "Could not find any entry info for: \n" + report2;
            if (!string.IsNullOrWhiteSpace(report3))
                report += "Users not found: \n" + report3;

            if (string.IsNullOrWhiteSpace(report))
                report = "All trade participants received their partners.";

            await user.SendMessageAsync(report);
        }
    }
}
