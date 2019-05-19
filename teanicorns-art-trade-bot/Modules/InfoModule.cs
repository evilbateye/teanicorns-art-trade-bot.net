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
        public CommandService CommandService { get; set; }

        [Command("about")]
        [Summary("Show info about this bot.")]
        public async Task ShowInfo()
        {
            var user = Context.Message.Author;
            string about = $"Hello <@{user.Id}>, I am art-trade-bot, written in Discord.Net ({DiscordConfig.Version}).\n";
            about += $"{Format.Bold("Command list:")}\n";

            bool adminUser = Utils.IsAdminUser(user);

            foreach (var cmd in CommandService.Commands)
            {
                if (Utils.IsAdminCommand(cmd))
                {
                    if (adminUser)
                        about += $"{Utils.adminGroupId} {cmd.Name} : {cmd.Summary}\n";
                }
                else
                    about += $"{cmd.Name} : {cmd.Summary}\n";
            }

            await ReplyAsync(about);
        }
    }
}
