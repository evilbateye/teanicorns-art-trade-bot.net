using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Discord;
using System.Text.RegularExpressions;

namespace teanicorns_art_trade_bot.Modules
{
    public class ReferenceModule : ModuleBase<SocketCommandContext>
    {
        [Command("set entry")]
        [Alias("se")]
        [Summary("Set your trade entry. (entry week only)")]
        public async Task SetEntry([Remainder]string description = null)
        {
            var user = Context.Message.Author;
            if (Storage.Axx.AppSettings.ArtTradeActive)
            {
                await ReplyAsync(string.Format(Properties.Resources.REF_TRADE_TAKING_PLACE, user.Id));
                return;
            }

            Storage.ApplicationData artHistory0 = TradeEventModule.GetAppDataFromHistory(0);
            if (artHistory0 != null)
            {
                Storage.UserData userData = TradeEventModule.GetMissingArt(artHistory0).Find(x => x.UserId == user.Id);
                if (userData != null)
                {
                    await ReplyAsync(string.Format(Properties.Resources.REF_TRADE_LAST_MONTH_ART_MISSING, user.Id));
                    return;
                }
            }

            var attachments = Context.Message.Attachments;
            if (attachments.Count <= 0 && string.IsNullOrWhiteSpace(description))
            {
                await ReplyAsync(string.Format(Properties.Resources.REF_TRADE_MISSING_REF, user.Id));
                return;
            }

            Storage.UserData data = new Storage.UserData();
            data.UserId = user.Id;
            data.UserName = user.Username;
            if (attachments.Count > 0)
                data.ReferenceUrl = attachments.FirstOrDefault().Url;
            if (!string.IsNullOrWhiteSpace(description))
                data.ReferenceDescription = description;
            if (user is IGuildUser guildUser)
                data.NickName = guildUser.Nickname;
            Storage.Axx.AppData.Set(data);
            await ReplyAsync(string.Format(Properties.Resources.REF_TRADE_REG_SUCC, user.Id));
        }

        [Command("get entry")]
        [Alias("ge")]
        [Summary("Get your trade entry.")]
        public async Task GetEntry()
        {
            var user = Context.Message.Author;
            var data = Storage.Axx.AppData.Get(user.Id);
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
            
            await ReplyAsync(string.Format(Properties.Resources.REF_NOT_REG, user.Id));
        }

        [Command("delete entry")]
        [Alias("de")]
        [Summary("Remove your trade entry. (entry week only)")]
        public async Task DeleteEntry()
        {
            var user = Context.Message.Author;
            if (Storage.Axx.AppSettings.ArtTradeActive)
            {
                await ReplyAsync(string.Format(Properties.Resources.REF_TRADE_TAKING_PLACE, user.Id));
                return;
            }
            
            if (Storage.Axx.AppData.Remove(user.Id))
                await ReplyAsync(string.Format(Properties.Resources.REF_REMOVED, user.Id));
            else
                await ReplyAsync(string.Format(Properties.Resources.REF_NOT_REG, user.Id));
        }

        [Command("show partner")]
        [Alias("sp")]
        [Summary("Sends you your trade partner's entry in a DM. (trade month only)")]
        public async Task ShowPartner()
        {
            var user = Context.Message.Author;
            if (!Storage.Axx.AppSettings.ArtTradeActive)
            {
                await ReplyAsync(string.Format(Properties.Resources.REF_EW_TAKING_PLACE, user.Id));
                return;
            }

            Storage.UserData nextUser;
            if (Storage.Axx.AppData.Next(user.Id, out nextUser))
            {
                if (!await SendPartnerResponse(nextUser, user))
                    await ReplyAsync(string.Format(Properties.Resources.REF_NOT_REG, user.Id));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.REF_MISSING_PARTNER, user.Id));
        }

        public static async Task<bool> SendPartnerResponse(Storage.UserData partnerData, Discord.WebSocket.SocketUser user)
        {
            Embed embed = null;
            if (!string.IsNullOrWhiteSpace(partnerData.ReferenceUrl))
                embed = new EmbedBuilder().WithImageUrl(partnerData.ReferenceUrl).Build();

            if (string.IsNullOrWhiteSpace(partnerData.ReferenceDescription) && embed == null)
                return false;


            string message = string.Format(Properties.Resources.REF_TRADE_PARTNER
                , user.Id
                , Format.Bold($"{partnerData.UserName}") + (string.IsNullOrWhiteSpace(partnerData.NickName) ? "" : $" ({partnerData.NickName})")) + "\n";

            if (!string.IsNullOrWhiteSpace(Storage.Axx.AppData.Theme))
                message += " " + string.Format(Properties.Resources.REF_TRADE_THEME, Storage.Axx.AppData.Theme) + "\n";

            if (!string.IsNullOrWhiteSpace(partnerData.ReferenceDescription))
                message += $"\"{partnerData.ReferenceDescription}\"";

            await user.SendMessageAsync(message, false, embed);
            return true;
        }

        [Command("reveal art")]
        [Alias("ra")]
        [Summary("Registers your finished art, sends DM with the art to your trade partner. (trade month only)")]
        public async Task RevealArt([Remainder]string text = null)
        {
            var user = Context.Message.Author;
            Storage.ApplicationData foundTrade = null;

            if (!string.IsNullOrWhiteSpace(text))
            {
                text = text.ToLower();

                for (int i = 0; i < Storage.Axx.AppHistory.History.Count && i < 3; ++i)
                {
                    var d = Storage.Axx.AppHistory.History[i];

                    if (string.IsNullOrWhiteSpace(d.Theme))
                        continue;
                    if (text.Contains(d.Theme.ToLower().Trim()))
                    {
                        foundTrade = d;
                        break;
                    }
                }
            }

            if (foundTrade == null)
            {
                foundTrade = Storage.Axx.AppData;
            }

            bool bCurrentTrade = foundTrade == Storage.Axx.AppData;

            if (bCurrentTrade && !Storage.Axx.AppSettings.ArtTradeActive)
            {
                await ReplyAsync(string.Format(Properties.Resources.REF_EW_TAKING_PLACE, user.Id));
                return;
            }

            var attachments = Context.Message.Attachments;
            if (attachments.Count <= 0)
            {
                await ReplyAsync(string.Format(Properties.Resources.REF_MISSING_ART, user.Id));
                return;
            }

            var data = foundTrade.Get(user.Id);
            if (data == null)
            {
                await ReplyAsync(string.Format(Properties.Resources.REF_NOT_REG, user.Id));
                return;
            }

            Storage.UserData nextUserData;
            if (foundTrade.Next(user.Id, out nextUserData))
            {
                data.ArtUrl = attachments.FirstOrDefault().Url;
                foundTrade.Set(data);

                if (!bCurrentTrade)
                    await GoogleDriveHandler.UploadGoogleFile(Storage.Axx.AppHistoryFileName);

                string reply = string.Format(Properties.Resources.REF_REVEAL_THANKS, user.Id);

                var client = Context.Client;
                var nextUser = client.GetUser(nextUserData.UserId);
                if (nextUser == null)
                {
                    await ReplyAsync(reply + " " + string.Format(Properties.Resources.REF_REVEAL_SORRY, user.Id));
                    return;
                }

                string monthTheme = "";
                if (!bCurrentTrade)
                    monthTheme = foundTrade.Theme;

                if (!await SendPartnerArtResponse(data, nextUser, monthTheme))
                    await ReplyAsync(reply + " " + string.Format(Properties.Resources.REF_REVEAL_SORRY, user.Id));
                else
                    await ReplyAsync(reply + " " + string.Format(Properties.Resources.REF_REVEAL_NOTIFY, nextUser.Id));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.REF_MISSING_PARTNER, user.Id));
        }

        public static async Task<bool> SendPartnerArtResponse(Storage.UserData partnerData, Discord.WebSocket.SocketUser user, string monthTheme)
        {
            Embed embed = null;
            if (!string.IsNullOrWhiteSpace(partnerData.ArtUrl))
                embed = new EmbedBuilder().WithImageUrl(partnerData.ArtUrl).Build();

            if (embed == null)
                return false;

            string message = string.Format(Properties.Resources.REF_REVEAL_FINAL, user.Id, partnerData.UserName
                + (string.IsNullOrWhiteSpace(partnerData.NickName) ? "" : $" ({partnerData.NickName})"));
            await user.SendMessageAsync(message, false, embed);
            return true;
        }
    }
}
