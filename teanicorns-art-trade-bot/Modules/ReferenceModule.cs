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
        [Summary("set your trade entry (entry week only)")]
        [InfoModule.SummaryDetail("available only when entry week is currently taking place" +
            "\nyou can add an image for the entry by embedding it into the message" +
            "\nyou can also add optional text description")]
        public async Task SetEntry([Remainder][Summary("description of your art trade entry (optional)")] string description = null)
        {
            var user = Context.Message.Author;
            if (Storage.xs.Settings.IsTradeMonthActive())
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "**trade** already started, entries can't be changed anymore"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            Storage.ApplicationData artHistory0 = TradeEventModule.GetAppDataFromHistory(0);
            if (artHistory0 != null)
            {
                Storage.UserData userData = TradeEventModule.GetMissingArt(artHistory0).Find(x => x.UserId == user.Id);
                if (userData != null)
                {
                    await ReplyAsync(string.Format(Properties.Resources.REF_TRADE_LAST_MONTH_ART_MISSING, user.Id, artHistory0.GetTheme())
                        + $"\n{string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, $"reveal art {artHistory0.GetTheme()}", "to register the missing art")}", embed: Utils.EmbedFooter(Context.Client));
                    return;
                }
            }

            var attachments = Context.Message.Attachments;
            if (attachments.Count <= 0 && string.IsNullOrWhiteSpace(description))
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_MISSING_INPUT, user.Id, "description and/or embeded image"), embed: Utils.EmbedFooter(Context.Client));
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

            Storage.xs.Entries.Set(data);

            await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "I saved your entry"), embed: Utils.EmbedFooter(Context.Client));
        }

        [Command("get entry")]
        [Alias("ge")]
        [Summary("get your trade entry")]
        [InfoModule.SummaryDetail("shows you the image and/or description that you have set as your trade entry")]
        public async Task GetEntry()
        {
            var user = Context.Message.Author;
            var data = Storage.xs.Entries.Get(user.Id);
            if (data != null)
            {
                Embed embed = null;
                if (!string.IsNullOrWhiteSpace(data.ReferenceUrl))
                    embed = Utils.GetFooterBuilder(Context.Client).WithImageUrl(data.ReferenceUrl).Build();
                                
                if (!string.IsNullOrWhiteSpace(data.ReferenceDescription) || embed != null)
                {
                    if (embed == null)
                        embed = Utils.EmbedFooter(Context.Client);

                    await ReplyAsync($"{data?.ReferenceDescription}", false, embed);
                    return;
                }
            }

            await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "entry"), embed: Utils.EmbedFooter(Context.Client));
        }

        [Command("delete entry")]
        [Alias("de", "remove entry", "rm")]
        [Summary("remove your trade entry (entry week only)")]
        [InfoModule.SummaryDetail("available only when entry week is currently taking place" +
            "\nremoves the image and/or description that you have set as your trade entry" +
            "\nusefull if you decide not to participate in the trade")]
        public async Task DeleteEntry()
        {
            var user = Context.Message.Author;
            if (Storage.xs.Settings.IsTradeMonthActive())
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "**trade** already started, entries can't be changed anymore"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            if (Storage.xs.Entries.Remove(user.Id))
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "your entry has been deleted"), embed: Utils.EmbedFooter(Context.Client));
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "entry"), embed: Utils.EmbedFooter(Context.Client));
        }

        [Command("show partner")]
        [Alias("sp")]
        [Summary("sends you your trade partner's entry in a DM (trade month only)")]
        [InfoModule.SummaryDetail("available only when trade month is currently taking place" +
            "\nsends you the image and/or description that your randomly selected trade partner has set for his trade" +
            "\nthe information is send using a direct message so that your partner won't see this")]
        public async Task ShowPartner()
        {
            var user = Context.Message.Author;
            if (!Storage.xs.Settings.IsTradeMonthActive())
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "entry week in progress, partners haven't been assigned yet"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            Storage.UserData nextUser;
            if (Storage.xs.Entries.Next(user.Id, out nextUser))
            {
                if (!await SendPartnerResponse(Context.Client, nextUser, user))
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "entry"), embed: Utils.EmbedFooter(Context.Client));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "partner"), embed: Utils.EmbedFooter(Context.Client));
        }

        public static async Task<bool> SendPartnerResponse(DiscordSocketClient client, Storage.UserData partnerData, Discord.WebSocket.SocketUser user, bool bThemeOnly = false)
        {
            if (bThemeOnly)
            {
                var message = (string.IsNullOrWhiteSpace(Storage.xs.Entries.GetTheme()) ? "none" : string.Format(Properties.Resources.TRADE_THIS_THEME, Storage.xs.Entries.GetTheme())) + "\n";
                await user.SendMessageAsync(message);
            }
            else
            {
                Embed embed = null;
                if (!string.IsNullOrWhiteSpace(partnerData.ReferenceUrl))
                    embed = Utils.GetFooterBuilder(client).WithImageUrl(partnerData.ReferenceUrl).Build();

                if (string.IsNullOrWhiteSpace(partnerData.ReferenceDescription) && embed == null)
                    return false;

                if (embed == null)
                    embed = Utils.EmbedFooter(client);

                string message = string.Format(Properties.Resources.REF_TRADE_PARTNER
                    , user.Id
                    , $"{partnerData.UserName}" + (string.IsNullOrWhiteSpace(partnerData.NickName) ? "" : $" ({partnerData.NickName})"));

                if (!string.IsNullOrWhiteSpace(Storage.xs.Entries.GetTheme()))
                    message += $"\n{string.Format(Properties.Resources.TRADE_THIS_THEME, Storage.xs.Entries.GetTheme())}";

                if (Storage.xs.Settings.GetTradeDays() != 0)
                    message += $"\n{string.Format(Properties.Resources.TRADE_ENDS_ON, Storage.xs.Settings.GetTradeDays(), Storage.xs.Settings.GetTradeStart(Storage.xs.Settings.GetTradeDays()).ToString("dd-MMMM"))}";

                if (!string.IsNullOrWhiteSpace(partnerData.ReferenceDescription))
                    message += $"\ndescription: `{partnerData.ReferenceDescription}`";

                await user.SendMessageAsync(message, false, embed);
            }

            return true;
        }

        [Command("reveal art")]
        [Alias("ra")]
        [Summary("registers your finished art, sends DM with the art to your trade partner (trade month only)")]
        [InfoModule.SummaryDetail("available only when trade month is currently taking place" +
            "\nyou can add an image for the art by embedding it into the message" +
            "\nyou can also add optional text param specifying the trade's theme" +
            "\nthis is usefull if you won't be able to submit the art on time, and another trade already started, you can specify the theme of the previous trade so that the bot understands where to register your art" +
            "\nthe art is sent to your trade partner in a direct message")]
        public async Task RevealArt([Remainder][Summary("theme of the art trade to which you want to register your art (optional)")]string theme = null)
        {
            var user = Context.Message.Author;
            Storage.ApplicationData foundTrade = null;

            if (!string.IsNullOrWhiteSpace(theme))
            {
                theme = theme.ToLower();

                for (int i = 0; i < Storage.xs.History.Count() && i < 3; ++i)
                {
                    var d = Storage.xs.History.GetTrade(i);
                    if (d == null)
                        continue;
                    if (string.IsNullOrWhiteSpace(d.GetTheme()))
                        continue;
                    if (!theme.Contains(d.GetTheme().ToLower().Trim()))
                        continue;

                    foundTrade = d;
                    break;
                }
            }

            if (foundTrade == null)
                foundTrade = Storage.xs.Entries;

            bool bCurrentTrade = foundTrade == Storage.xs.Entries;

            if (bCurrentTrade && !Storage.xs.Settings.IsTradeMonthActive())
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "entry week in progress, partners haven't been assigned yet"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            var attachments = Context.Message.Attachments;
            if (attachments.Count <= 0)
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_MISSING_INPUT, user.Id, "embeded image"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            var data = foundTrade.Get(user.Id);
            if (data == null)
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "entry"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            Storage.UserData nextUserData;
            if (foundTrade.Next(user.Id, out nextUserData))
            {
                data.ArtUrl = attachments.FirstOrDefault().Url;
                foundTrade.Set(data);

                if (!bCurrentTrade)
                    await GoogleDriveHandler.UploadGoogleFile(Storage.xs.HISTORY_PATH);

                var client = Context.Client;
                var nextUser = client.GetUser(nextUserData.UserId);
                if (nextUser == null)
                {
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "could not find your partner"), embed: Utils.EmbedFooter(Context.Client));
                    return;
                }

                string monthTheme = "";
                if (!bCurrentTrade)
                    monthTheme = foundTrade.GetTheme();

                if (await SendPartnerArtResponse(Context.Client, data, nextUser, monthTheme))
                    await ReplyAsync(string.Format(Properties.Resources.REF_REVEAL_NOTIFY, user.Id, nextUser.Id), embed: Utils.EmbedFooter(Context.Client));
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "could not notify your partner"), embed: Utils.EmbedFooter(Context.Client));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "partner"), embed: Utils.EmbedFooter(Context.Client));
        }

        public static async Task<bool> SendPartnerArtResponse(DiscordSocketClient client, Storage.UserData partnerData, SocketUser user, string monthTheme)
        {
            Embed embed = null;
            if (!string.IsNullOrWhiteSpace(partnerData.ArtUrl))
                embed = Utils.GetFooterBuilder(client).WithImageUrl(partnerData.ArtUrl).Build();

            if (embed == null)
                return false;

            string message = string.Format(Properties.Resources.REF_REVEAL_FINAL, user.Id, partnerData.UserName
                + (string.IsNullOrWhiteSpace(partnerData.NickName) ? "" : $" ({partnerData.NickName})"));
            await user.SendMessageAsync(message, embed: embed);
            return true;
        }

        [Command("add theme")]
        [Alias("ath", "set theme", "sth")]
        [Summary("adds a theme to the theme pool (entry week only)")]
        [InfoModule.SummaryDetail("available only when entry week is currently taking place" +
           "\nduring the entry week there will be a poll and the trade participants will choose which theme they like the most" +
           "\nthe poll will take until the entry week ends, theme with the most votes wins" +
           "\nif there is more then 1 theme with the most ammount of votes, then the theme is chosen from them by random")]
        public async Task AddTheme([Summary("the theme name (you can place optional emoji into the theme name, which will be used during theme poll instead of default emojis)")][Remainder]string theme)
        {
            var user = Context.Message.Author;
            if (!Storage.xs.Settings.IsEntryWeekActive())
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "**trade** already started, entries can't be changed anymore"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            if (Storage.xs.Settings.IsThemePoolMaxed())
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_MAX_NUM_OF_ARGS, user.Id, "themes"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            if (Storage.xs.Settings.AddThemeToPool(user.Id, theme))
            {
                await Utils.EditThemePoll(Context.Client);
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "your theme has been registered"), embed: Utils.EmbedFooter(Context.Client));
            }
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_DUPLICAT_ARG, user.Id, "a theme"), embed: Utils.EmbedFooter(Context.Client));

        }

        [Command("delete theme")]
        [Alias("dth", "remove theme", "rmth")]
        [Summary("removes a theme from your theme pool (entry week only)")]
        public async Task DeleteTheme([Summary("the name of the theme to be removed")][Remainder]string theme)
        {
            var user = Context.Message.Author;
            if (!Storage.xs.Settings.IsEntryWeekActive())
            {
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "**trade** already started, entries can't be changed anymore"), embed: Utils.EmbedFooter(Context.Client));
                return;
            }

            if (Utils.IsAdminUser(user))
            {
                if (Storage.xs.Settings.RemoveThemeFromPool(theme))
                {
                    await Utils.EditThemePoll(Context.Client);
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "the theme has been unregistered"), embed: Utils.EmbedFooter(Context.Client));
                }
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to remove theme from pool"), embed: Utils.EmbedFooter(Context.Client));
            }
            else
            {
                if (Storage.xs.Settings.RemoveThemeFromPool(user.Id, theme))
                {
                    await Utils.EditThemePoll(Context.Client);
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "your theme has been unregistered"), embed: Utils.EmbedFooter(Context.Client));
                }
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to remove theme from pool"), embed: Utils.EmbedFooter(Context.Client));
            }
        }

        [Command("show themes")]
        [Alias("sth")]
        [Summary("shows you a list of your registered themes")]
        public async Task ShowThemes()
        {
            var user = Context.Message.Author;

            List<Storage.ArtTheme> themes;
            if (!Storage.xs.Settings.GetThemePool(user.Id, out themes))
                themes = new List<Storage.ArtTheme>();

            await ReplyAsync($"{string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "here is a list of your registered themes")}: " +
                (themes.Count > 0 ? string.Join(", ", themes.Select(x => $"{x.EmojiCode} `{x.Theme}`")) : "`none`"), embed: Utils.EmbedFooter(Context.Client));
        }

        [Command("subscribe")]
        [Alias("sub")]
        [Summary("subscribe for trade notifications")]
        [InfoModule.SummaryDetail("subscribers will be notified by a direct message every time the bot sends an important announcement")]
        public async Task Notify([Summary("true = turn on notifications, false = turn them off, no argument = toggle")]bool? bOnOff = null)
        {
            var user = Context.Message.Author;
            if (Storage.xs.Settings.ChangeSubscription(user.Id, bOnOff))
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "you have been subscribed"), embed: Utils.EmbedFooter(Context.Client));
            else
                await ReplyAsync(string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to change subscription"), embed: Utils.EmbedFooter(Context.Client));
        }
    }
}
