using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord;

namespace teanicorns_art_trade_bot.Modules
{
    public class ReferenceModule : ModuleBase<SocketCommandContext>
    {
        [Command("set entry")]
        [Summary("Set your trade entry. [during entry week only]")]
        public async Task SetEntry([Remainder]string description = null)
        {
            var user = Context.Message.Author;
            if (PersistentStorage.AppData.ArtTradeActive)
            {
                await ReplyAsync($"Sorry <@{user.Id}>. Art trade is currently taking place. Can't modify existing entries.");
                return;
            }

            var attachments = Context.Message.Attachments;
            if (attachments.Count <= 0 && string.IsNullOrWhiteSpace(description))
            {
                await ReplyAsync($"Missing reference <@{user.Id}>. Please provide a description and/or embeded image.");
                return;
            }

            PersistentStorage.UserData data = new PersistentStorage.UserData();
            data.UserId = user.Id;
            data.UserName = user.Username;
            if (attachments.Count > 0)
                data.ReferenceUrl = attachments.FirstOrDefault().Url;
            if (!string.IsNullOrWhiteSpace(description))
                data.ReferenceDescription = description;
            PersistentStorage.Set(data);
            await ReplyAsync($"Your entry has been registered successfully <@{user.Id}>!");
        }

        [Command("get entry")]
        [Summary("Get your trade entry.")]
        public async Task GetEntry()
        {
            var user = Context.Message.Author;
            var data = PersistentStorage.Get(user.Id);
            if (data != null)
            {
                Embed embed = null;
                if (!string.IsNullOrWhiteSpace(data.ReferenceUrl))
                    embed = new EmbedBuilder().WithImageUrl(data.ReferenceUrl).Build();

                if (!string.IsNullOrWhiteSpace(data.ReferenceDescription) || embed != null)
                {
                    await ReplyAsync($"{data?.ReferenceDescription}", false, embed);
                    return;
                }
            }

            await ReplyAsync($"Sorry <@{user.Id}>, there is no reference registered.");
        }

        [Command("delete entry")]
        [Summary("Remove your trade entry. [during entry week only]")]
        public async Task DeleteEntry()
        {
            var user = Context.Message.Author;
            if (PersistentStorage.AppData.ArtTradeActive)
            {
                await ReplyAsync($"Sorry <@{user.Id}>. Art trade is currently taking place. Can't modify existing entries.");
                return;
            }

            if (PersistentStorage.Remove(user.Id))
                await ReplyAsync($"<@{user.Id}> your reference has been removed.");
            else
                await ReplyAsync($"Sorry <@{user.Id}>, there is no reference registered.");
        }

        [Command("show partner")]
        [Summary("Sends you your trade partner's entry in a DM. [during trade month only]")]
        public async Task ShowPartner()
        {
            var user = Context.Message.Author;
            if (!PersistentStorage.AppData.ArtTradeActive)
            {
                await ReplyAsync($"Sorry <@{user.Id}>. Entry week is currently taking place. Trade pairs are not formed until closed.");
                return;
            }

            PersistentStorage.UserData nextUser;
            if (PersistentStorage.Next(user.Id, out nextUser))
            {
                if (!await SendPartnerResponse(nextUser, user))
                    await ReplyAsync($"Sorry <@{user.Id}>, your art trade partner has no reference registered.");
            }
            else
                await ReplyAsync($"Sorry <@{user.Id}>. Could not find an art trade partner for you.");
        }

        public static async Task<bool> SendPartnerResponse(PersistentStorage.UserData partnerData, Discord.WebSocket.SocketUser user)
        {
            Embed embed = null;
            if (!string.IsNullOrWhiteSpace(partnerData.ReferenceUrl))
                embed = new EmbedBuilder().WithImageUrl(partnerData.ReferenceUrl).Build();

            if (string.IsNullOrWhiteSpace(partnerData.ReferenceDescription) && embed == null)
                return false;

            string message = $"Your art trade partner is.. {Format.Bold($"{partnerData.UserName}")}.";
            if (!string.IsNullOrWhiteSpace(PersistentStorage.AppData.Theme))
                message += $" Theme of this art trade is.. {PersistentStorage.AppData.Theme}.";
            message += $" Have fun <@{user.Id}>!\n";
            if (!string.IsNullOrWhiteSpace(partnerData.ReferenceDescription))
                message += $"\"{partnerData.ReferenceDescription}\"";

            await user.SendMessageAsync(message, false, embed);
            return true;
        }
    }
}
