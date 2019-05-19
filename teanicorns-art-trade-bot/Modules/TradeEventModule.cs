﻿using System;
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
    [Group(Utils.adminGroupId)]
    public class TradeEventModule : ModuleBase<SocketCommandContext>
    {
        [Command("entry week")]
        [Summary("Stops the art trade, starts accepting entries.")]
        public async Task EntryWeek()
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            if (PersistentStorage.ActivateTrade(false))
                await ReplyAsync($"@everyone the entry week started. {Format.Bold("We are accepting new entries!")}");
            else
                await ReplyAsync($"<@{Context.Message.Author.Id}> the entry week is already in progress.");
        }

        [Command("trade month")]
        [Summary("Starts the art trade, stops accepting entries. [updates backup]")]
        public async Task TradeMonth()
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            if (PersistentStorage.ActivateTrade(true))
            {
                PersistentStorage.BackupStorage();
                await ReplyAsync($"@everyone the art trade month started. {Format.Bold("We are no longer accepting new entries!")}");
            }
            else
                await ReplyAsync($"<@{Context.Message.Author.Id}> the art trade month is already in progress.");
        }

        [Command("shuffle")]
        [Summary("Randomly shuffle art trade entries.")]
        public async Task Shuffle()
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            PersistentStorage.Shuffle();
            await ReplyAsync($"Entries have been shuffled successfully <@{user.Id}>.");
        }

        [Command("clear")]
        [Summary("Delete all art trade entries. [updates backup]")]
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

        [Command("swap")]
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

            if (PersistentStorage.ResetNext(partner2.Id, partner1.Id))
                await ourUser.SendMessageAsync($"Art trade partner of {Format.Bold(partner2.Username)} has been changed to {Format.Bold(partner1.Username)} <@{ourUser.Id}>.");
            else
                await ReplyAsync($"Could not change your art trade partner.");
        }

        [Command("list")]
        [Summary("Sends you a list of all entries in a DM.")]
        public async Task ListAllEntries(string all = null)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            string entries = $"Listing all entries <@{user.Id}>. Each next entry is the partner of the previous one.\n";
            if (string.IsNullOrWhiteSpace(all) || all != "all")
                entries += string.Join("\n", PersistentStorage.GetStorage().Select(x => $"{x.UserName}"));
            else
                entries += string.Join("\n", PersistentStorage.GetStorage().Select(x => $"{x.UserName}\n{x.UserId}\n" +
                (string.IsNullOrWhiteSpace(x.ReferenceUrl) ? "" : $"<{x.ReferenceUrl}>\n") +
                (string.IsNullOrWhiteSpace(x.ReferenceDescription) ? "" : $"{x.ReferenceDescription}\n")));
            await user.SendMessageAsync(entries);
        }

        [Command("work channel")]
        [Summary("Sets the working channel for ATB.")]
        public async Task WorkChannel(string channel)
        {
            var user = Context.Message.Author;
            if (!Utils.IsAdminUser(user))
            {
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. You don't have required priviledges to run this command.");
                return;
            }

            if (PersistentStorage.SetWorkingChannel(channel))
                await ReplyAsync($"Channel has been set successfully <@{Context.Message.Author.Id}>.");
            else
                await ReplyAsync($"Sorry <@{Context.Message.Author.Id}>. Was not able to change the working channel. Current working channel is {PersistentStorage.AppData.WorkingChannel}.");
        }

        [Command("restore")]
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
    }
}
