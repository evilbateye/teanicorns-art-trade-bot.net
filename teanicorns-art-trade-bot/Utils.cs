using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Commands;

namespace teanicorns_art_trade_bot
{
    public class Utils
    {
        public const string adminGroupId = "admin";

        public static bool IsAdminUser(SocketUser user)
        {
            if (user is SocketGuildUser guildUser)
                return guildUser.GuildPermissions.Administrator;

            var guild = user.MutualGuilds.FirstOrDefault();
            if (guild == null)
                return false;

            var userList = guild.Users.ToList();
            int index = userList.FindIndex(x => x.Id == user.Id);
            if (index == -1)
                return false;

            return userList[index].GuildPermissions.Administrator;
        }

        public static bool IsAdminCommand(CommandInfo cmd)
        {
            //return cmd.Module.Group == adminGroupId;
            return cmd.Module.Name == "TradeEventModule";
        }

        public static SocketUser FindUser(SocketGuild guild, string userId)
        {
            int index = -1;
            var userList = guild.Users.ToList();
            
            if (UInt64.TryParse(userId, out ulong numericId))
                index = userList.FindIndex(x => x.Id == numericId);
            else
                index = userList.FindIndex(x => x.Username == userId || x.Nickname == userId);

            return index == -1 ? null : userList[index];
        }

        public static SocketGuild FindGuild(SocketUser user)
        {
            if (user is SocketGuildUser guildUser)
                return guildUser.Guild;

            return user.MutualGuilds.FirstOrDefault();
        }

        public static SocketGuild FindGuild(DiscordSocketClient socketClient)
        {
            return socketClient.Guilds.FirstOrDefault();
        }

        public static SocketTextChannel FindChannel(DiscordSocketClient socketClient, string channel)
        {
            SocketGuild guild = FindGuild(socketClient);
            if (guild == null)
                return null;
            if (string.IsNullOrWhiteSpace(channel))
                channel = "general";
            return FindChannel(guild, channel);
        }

        public static SocketTextChannel FindChannel(SocketGuild guild, string channel)
        {
            if (guild == null)
                return null;

            foreach (SocketTextChannel txtChannel in guild.TextChannels)
            {
                if (txtChannel.Name == channel)
                    return txtChannel;
            }

            return null;
        }
    }
}
