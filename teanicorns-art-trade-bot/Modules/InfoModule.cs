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
        [Alias("a")]
        [Summary("show info about the art trade bot")]
        public async Task ShowInfo([Remainder][Summary("name of the command you want more detailed info about (optional)")]string cmd_name = null)
        {
            var user = Context.Message.Author;

            if (string.IsNullOrWhiteSpace(cmd_name))
            {
                string about = $"\n{string.Format(Properties.Resources.INFO_INTRO, user.Id, DiscordConfig.Version, Config.CmdPrefix, "about", "set entry")}\n";
                about += $"\n{string.Format(Properties.Resources.INFO_CMD_LIST)}\n";
                string adminAbout = $"\n{string.Format(Properties.Resources.INFO_ADMIN_CMD_LIST)}\n";
                bool adminUser = Utils.IsAdminUser(user);

                foreach (var cmd in CommandService.Commands)
                {
                    var par = cmd.Parameters;

                    string aliases = $"`{Config.CmdPrefix}{cmd.Aliases.FirstOrDefault()}`";
                    if (!string.IsNullOrWhiteSpace(aliases))
                    {
                        for (int i = 1; i < cmd.Aliases.Count; ++i)
                            aliases += $" | `{Config.CmdPrefix}{cmd.Aliases.ElementAt(i)}`";
                    }

                    if (Utils.IsAdminCommand(cmd))
                    {
                        if (adminUser)
                            adminAbout += $"{aliases} : {cmd.Summary}\n";
                    }
                    else
                        about += $"{aliases} : {cmd.Summary}\n";
                }

                await ReplyAsync(about + adminAbout);
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

                    string about = $"{aliases} : {match.Summary}\n";
                    var attrib = match.Attributes.FirstOrDefault(x => x is SummaryDetail);
                    if (attrib != null)
                        about += $"{attrib.ToString()}\n";

                    if (match.Parameters.Count > 0)
                    {
                        about += $"\n{string.Format(Properties.Resources.INFO_PARAM_CMD_LIST)}";
                        foreach (var param in match.Parameters)
                        {
                            about += $"\n`{param.Name}` : {param.Summary}";
                        }
                    }
                    await ReplyAsync(about);
                }
                else
                    await ReplyAsync(string.Format(Properties.Resources.GLOBAL_UNKNOW_ARG, user.Id));
            }
        }
    }
}
