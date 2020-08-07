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
        [Summary("set image reference and/or description for the person that will be doing art for you")]
        [InfoModule.SummaryDetail("available during `entry week only`" +
            "\nyou can attach an image reference by `embedding` it into the message, you can also add `optional text` description" +
            "\n\n**Usage** : basic command, every user needs to use this command if they want to enter the art trade" +
            "\nif you want to `update` the already registered image reference and/or description, you `don't` have to delete the entry and create it again" +
            "\ninstead you can run the `set entry` command providing only the `new updated version` of the image reference and/or description")]
        public async Task SetEntry([Remainder][Summary("description of your art trade entry (`optional`)")] string description = null)
        {
            var user = Context.Message.Author;
            if (Storage.xs.Settings.IsTradeMonthActive())
            {
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "**trade** already started, entries can't be changed anymore")));
                return;
            }

            Storage.ApplicationData artHistory0 = TradeEventModule.GetAppDataFromHistory(0);
            if (artHistory0 != null)
            {
                Storage.UserData userData = TradeEventModule.GetMissingArt(artHistory0).Find(x => x.UserId == user.Id);
                if (userData != null)
                {
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.REF_TRADE_LAST_MONTH_ART_MISSING, user.Id, artHistory0.GetTheme())
                        + $"\n{string.Format(Properties.Resources.GLOBAL_CMDHELP, Config.CmdPrefix, $"reveal art {artHistory0.GetTheme()}", "register the missing art and I will let you enter")}"));
                    return;
                }
            }

            var attachments = Context.Message.Attachments;
            if (attachments.Count <= 0 && string.IsNullOrWhiteSpace(description))
            {
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_MISSING_INPUT, user.Id, "description and/or embeded image")));
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

            await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "I saved your entry")));
        }

        [Command("get entry")]
        [Alias("ge")]
        [Summary("show your registered entry information")]
        [InfoModule.SummaryDetail("shows you the `image reference` and/or text `description` that you have provided for the person that will be doing art for you")]
        public async Task GetEntry()
        {
            var user = Context.Message.Author;
            var data = Storage.xs.Entries.Get(user.Id);
            if (data != null)
            {
                if (!string.IsNullOrWhiteSpace(data.ReferenceDescription) || !string.IsNullOrWhiteSpace(data.ReferenceUrl))
                {
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, data.ReferenceDescription, data.ReferenceUrl));
                    return;
                }
            }

            await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "entry")));
        }

        [Command("remove entry")]
        [Alias("rm")]
        [Summary("remove your registered entry information")]
        [InfoModule.SummaryDetail("available during `entry week only`" +
            "\nremoves the `image reference` and/or text `description` that you have provided for the person that will be doing art for you" +
            "\n\n**Usage** : useful if you decide not to participate in the art trade before the trade starts")]
        public async Task DeleteEntry()
        {
            var user = Context.Message.Author;
            if (Storage.xs.Settings.IsTradeMonthActive())
            {
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "**trade** already started, entries can't be changed anymore")));
                return;
            }

            if (Storage.xs.Entries.Remove(user.Id))
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "your entry has been deleted")));
            else
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "entry")));
        }

        [Command("show partner")]
        [Alias("sp")]
        [Summary("show info about the person you are drawing for in a direct message")]
        [InfoModule.SummaryDetail("available during `trade month only`" +
            "\nsends you a `direct message` with the `image reference` and/or text `description` that the person you are drawing for provided for you" +
            "\n\n**Usage** : usually not needed, as the bot sends this information automatically")]
        public async Task ShowPartner()
        {
            var user = Context.Message.Author;
            if (!Storage.xs.Settings.IsTradeMonthActive())
            {
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "entry week in progress, partners haven't been assigned yet")));
                return;
            }

            Storage.UserData nextUser;
            if (Storage.xs.Entries.Next(user.Id, out nextUser))
            {
                if (!await SendPartnerResponse(Context.Client, nextUser, user))
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "entry")));
            }
            else
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "partner")));
        }

        public static async Task<bool> SendPartnerResponse(DiscordSocketClient client, Storage.UserData partnerData, Discord.WebSocket.SocketUser user, bool bThemeOnly = false)
        {
            if (bThemeOnly)
            {
                var message = (string.IsNullOrWhiteSpace(Storage.xs.Entries.GetTheme()) ? "none" : string.Format(Properties.Resources.TRADE_THIS_THEME, Storage.xs.Entries.GetTheme())) + "\n";
                await user.SendMessageAsync(embed: Utils.EmbedMessage(client, message));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(partnerData.ReferenceDescription) && string.IsNullOrWhiteSpace(partnerData.ReferenceUrl))
                    return false;

                string message = string.Format(Properties.Resources.REF_TRADE_PARTNER, user.Id, $"{partnerData.UserName}" + (string.IsNullOrWhiteSpace(partnerData.NickName) ? "" : $" ({partnerData.NickName})"));

                if (!string.IsNullOrWhiteSpace(Storage.xs.Entries.GetTheme()))
                    message += $"\n{string.Format(Properties.Resources.TRADE_THIS_THEME, Storage.xs.Entries.GetTheme())}";

                if (Storage.xs.Settings.GetTradeDays() != 0)
                    message += $"\n{string.Format(Properties.Resources.TRADE_ENDS_ON, Storage.xs.Settings.GetTradeDays(), Storage.xs.Settings.GetTradeStart(Storage.xs.Settings.GetTradeDays()).ToString("dd-MMMM"))}";

                if (!string.IsNullOrWhiteSpace(partnerData.ReferenceDescription))
                    message += $"\n`description` : *\"{partnerData.ReferenceDescription}\"*";

                await user.SendMessageAsync(embed: Utils.EmbedMessage(client, message, partnerData.ReferenceUrl));
            }

            return true;
        }

        [Command("reveal art")]
        [Alias("ra")]
        [Summary("register your art and notify the person you are drawing for in a direct message")]
        [InfoModule.SummaryDetail("available during `trade month only`" +
            "\nyou can attach an image reference by `embedding` it into the message, similar to how `set entry` command works" +
            "\nyou can also add `optional text` specifying the trade's `theme`" +
            "\n\n**Usage** : used to let the bot know that you have finished your art, and to notify the person you are drawing for" +
            "\nthe optional `theme` parameter is used if you weren't able to submit the art on time, and another art trade already started" +
            "\nthe bot won't let you reveal art during `entry week`, but by specifying the `theme` of the past art trade the bot will allow you to register the art for that trade")]
        public async Task RevealArt([Remainder][Summary("theme of the art trade for which you want to register your art (`optional`)")]string theme = null)
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
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "entry week in progress, partners haven't been assigned yet")));
                return;
            }

            var attachments = Context.Message.Attachments;
            if (attachments.Count <= 0)
            {
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_MISSING_INPUT, user.Id, "embeded image")));
                return;
            }

            var data = foundTrade.Get(user.Id);
            if (data == null)
            {
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "entry")));
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
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "could not find your partner")));
                    return;
                }

                string monthTheme = "";
                if (!bCurrentTrade)
                    monthTheme = foundTrade.GetTheme();

                if (await SendPartnerArtResponse(Context.Client, data, nextUser, monthTheme))
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.REF_REVEAL_NOTIFY, user.Id, nextUser.Id)));
                else
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "could not notify your partner")));
            }
            else
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ITEM_NOT_FOUND, user.Id, "partner")));
        }

        public static async Task<bool> SendPartnerArtResponse(DiscordSocketClient client, Storage.UserData partnerData, SocketUser user, string monthTheme)
        {
            if (string.IsNullOrWhiteSpace(partnerData.ArtUrl))
                return false;

            string message = string.Format(Properties.Resources.REF_REVEAL_FINAL, user.Id, partnerData.UserName
                + (string.IsNullOrWhiteSpace(partnerData.NickName) ? "" : $" ({partnerData.NickName})"), monthTheme);

            await user.SendMessageAsync(embed: Utils.EmbedMessage(client, message, partnerData.ArtUrl));
            return true;
        }

        [Command("add theme")]
        [Alias("ath")]
        [Summary("add a theme to your themes list")]
        [InfoModule.SummaryDetail("the themes are used for a `poll` which will be taking place during `entry week`" +
           "\nthe poll will be available until the `entry week ends`, during this time trade participants can vote for the upcoming trade's theme" +
           "\nif there is more than `1` theme with the most ammount of votes, then the theme is chosen from them by `random`")]
        public async Task AddTheme([Summary("the theme name, you can place an `optional emoji` into the theme name, which will replace the default one")][Remainder]string theme)
        {
            var user = Context.Message.Author;
            if (Storage.xs.Settings.IsThemePoolMaxed())
            {
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_MAX_NUM_OF_ARGS, user.Id, "themes")));
                return;
            }

            if (Storage.xs.Settings.AddThemeToPool(user.Id, theme))
            {
                await Utils.EditThemePoll(Context.Client);
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "your theme has been registered")));
            }
            else
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_DUPLICAT_ARG, user.Id, "a theme")));

        }

        [Command("remove theme")]
        [Alias("rmth")]
        [Summary("remove a theme from your themes list")]
        [InfoModule.SummaryDetail("removes the theme but also all the reaction votes your theme might have")]
        public async Task DeleteTheme([Summary("the name of the theme to be removed")][Remainder]string theme)
        {
            var user = Context.Message.Author;
            if (Utils.IsAdminUser(user))
            {
                if (Storage.xs.Settings.RemoveThemeFromPool(theme))
                {
                    await Utils.EditThemePoll(Context.Client);
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "the theme has been unregistered")));
                }
                else
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to remove theme from pool")));
            }
            else
            {
                if (Storage.xs.Settings.RemoveThemeFromPool(user.Id, theme))
                {
                    await Utils.EditThemePoll(Context.Client);
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "your theme has been unregistered")));
                }
                else
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to remove theme from pool")));
            }
        }

        [Command("themes")]
        [Summary("show a list of your themes")]
        [InfoModule.SummaryDetail("sends you a list of your registered themes")]
        public async Task ShowThemes()
        {
            var user = Context.Message.Author;

            List<Storage.ArtTheme> themes;
            if (!Storage.xs.Settings.GetThemePool(user.Id, out themes))
                themes = new List<Storage.ArtTheme>();

            await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, $"{string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "here is a list of your registered themes")}: " +
                (themes.Count > 0 ? string.Join(", ", themes.Select(x => $"{x.EmojiCode} `{x.Theme}`")) : "`none`")));
        }

        [Command("subscribe")]
        [Alias("sub")]
        [Summary("subscribe for additional direct message bot notifications")]
        [InfoModule.SummaryDetail("subscribes you for direct message notifications from the bot (`entry week start`, `trade month start`, ...)")]
        public async Task Subscribe([Summary("true = turn `on` notifications, false = turn them `off`, no argument = `toggle`")]bool? bOnOff = null)
        {
            var user = Context.Message.Author;
            if (Storage.xs.Settings.ChangeSubscription(user.Id, ref bOnOff))
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, $"you have been {(bOnOff.Value ? string.Empty : "un-")}subscribed")));
            else
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "unable to change subscription")));
        }

        [Command("ping")]
        [Summary("incognito message the person you are drawing for")]
        [InfoModule.SummaryDetail("available during `trade month only`" +
            "\nsends a `direct message` to the person you are drawing for" +
            "\n\n**Usage** : usefull if you are not certain of the information the person you are drawing for provided, and you need to verify with them" +
            "\nyou also don't want them to know that you are the one asking and spoil the surprise, so the bot will ask in your stead" +
            "\n\n**Example** : Alice `sees` Bob as her trade partner, Bob `does not know` Alice is making art for him" +
            "\nAlice writes `ping Hello!` and Bob receives this message not knowing who sent it to him" +
            "\nBob replies back by writing `pong Hello back!` and the message is forwarded back to Alice")]
        public async Task Ping([Summary("the message that will be sent to the person you are drawing for")][Remainder]string message)
        {
            var user = Context.Message.Author;
            if (!Storage.xs.Settings.IsTradeMonthActive())
            {
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "entry week in progress, partners haven't been assigned yet")));
                return;
            }

            Storage.UserData nextUserData;
            if (Storage.xs.Entries.Next(user.Id, out nextUserData))
            {
                var nextUser = Context.Client.GetUser(nextUserData.UserId);
                if (nextUser == null)
                {
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "could not find trade partner")));
                    return;
                }

                await nextUser.SendMessageAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.TRADE_PINGPONG, nextUser.Id, "the person doing art for you", Config.CmdPrefix, "pong <reply>", message)));

                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "the message has been forwarded")));
            }
            else
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "could not find trade partner")));
        }

        [Command("pong")]
        [Summary("incognito message the person doing art for you")]
        [InfoModule.SummaryDetail("available during `trade month only`" +
            "\nsends a `direct message` to the person doing art for you" +
            "\n\n**Usage** : usefull if you are not certain of the information you provided for the art trade, and you want to send some more info to the person doing art for you" +
            "\nthis command is used to reply to the `ping` command" +
            "\n\n**Example** : Alice `sees` Bob as her trade partner, Bob `does not know` Alice is making art for him" +
            "\nAlice writes `ping Hello!` and Bob receives this message not knowing who sent it to him" +
            "\nBob replies back by writing `pong Hello back!` and the message is forwarded back to Alice")]
        public async Task Pong([Summary("the message that will be sent to the person doing art for you")][Remainder] string message)
        {
            var user = Context.Message.Author;
            if (!Storage.xs.Settings.IsTradeMonthActive())
            {
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "entry week in progress, partners haven't been assigned yet")));
                return;
            }

            Storage.UserData previousUserData;
            if (Storage.xs.Entries.Previous(user.Id, out previousUserData))
            {
                var previousUser = Context.Client.GetUser(previousUserData.UserId);
                if (previousUser == null)
                {
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "could not find trade partner")));
                    return;
                }

                await previousUser.SendMessageAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.TRADE_PINGPONG, previousUser.Id, "the person you are drawing for", Config.CmdPrefix, "ping <message>", message)));

                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_SUCCESS, user.Id, "the message has been forwarded")));
            }
            else
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_ERROR, user.Id, "could not find trade partner")));
        }
    }
}
