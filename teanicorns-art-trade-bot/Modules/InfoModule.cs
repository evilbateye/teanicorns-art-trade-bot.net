using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Discord.Commands;
using Discord;
using Discord.WebSocket;

namespace teanicorns_art_trade_bot.Modules
{
    public class InfoModule : ModuleBase<SocketCommandContext>
    {
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        public class SummaryDetail : Attribute
        {
            public SummaryDetail(string text) { Text = text; }
            public string Text { get; set; }
            public override string ToString() { return Text; }
        }

        public CommandService CommandService { get; set; }
        
        [Command("about")]
        [Alias("a", "help", "h")]
        [Summary("show info about the bot")]
        public async Task About([Remainder][Summary("name of the command you want more detailed info about (`optional`)")]string cmd_name = null)
        {
            var user = Context.Message.Author;

            if (string.IsNullOrWhiteSpace(cmd_name))
            {
                var aboutMsgs = Utils.CreateAbout(CommandService, Utils.IsAdminUser(user));
                await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Join("\n\n", aboutMsgs), Utils.Emotion.none));
            }
            else
            {
                CommandInfo match = null;
                cmd_name = cmd_name.ToLower().Trim();
                foreach (var cmd in CommandService.Commands)
                {
                    if (!string.IsNullOrWhiteSpace(cmd.Aliases.FirstOrDefault(x => cmd_name.Equals(x))))
                    {
                        match = cmd;
                        break;
                    }
                }

                if (match != null)
                {
                    string aliases = $"`{Config.CmdPrefix}{match.Aliases.FirstOrDefault()}`";
                    if (!string.IsNullOrWhiteSpace(aliases))
                    {
                        for (int i = 1; i < match.Aliases.Count; ++i)
                            aliases += $" | `{Config.CmdPrefix}{match.Aliases.ElementAt(i)}`";
                    }

                    string about = $"\n{aliases} : {match.Summary}";
                    var attrib = match.Attributes.FirstOrDefault(x => x is SummaryDetail);
                    if (attrib != null)
                        about += $"\n{attrib.ToString()}";

                    if (match.Parameters.Count > 0)
                    {
                        about += $"\n\n**Parameters**";
                        foreach (var param in match.Parameters)
                        {
                            about += $"\n`{param.Name}` : {param.Summary}";
                        }
                    }
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, about, Utils.Emotion.none));
                }
                else
                    await ReplyAsync(embed: Utils.EmbedMessage(Context.Client, string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, user.Id), Utils.Emotion.neutral));
            }
        }
    }
}
