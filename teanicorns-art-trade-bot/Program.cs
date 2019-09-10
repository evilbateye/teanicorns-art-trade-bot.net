using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Net;

using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;
using System.Threading;
using System.Globalization;

namespace teanicorns_art_trade_bot
{
	class Program
	{
        private DiscordSocketClient _discord;
        static void Main(string[] args)
		{
			new Program().MainAsync().GetAwaiter().GetResult();
		}

		public Program()
		{
		}

		public async Task MainAsync()
		{
            
            var services = ConfigureServices();

            _discord = services.GetRequiredService<DiscordSocketClient>();
            _discord.Log += Log;
            _discord.Ready += Ready;

            var command = services.GetRequiredService<CommandService>();
            command.Log += Log;

            var handler = services.GetRequiredService<CommandHandler>();
            await handler.Initialize();

            await _discord.LoginAsync(TokenType.Bot, Config.DiscordToken);
            await _discord.StartAsync();

            await GoogleDrive.SetupGoogleDrive(new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = Config.GoogleCred,
                ApplicationName = "teanicorns-art-trade-bot",
            }));

            //Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");

            Storage.Axx.Initialize();

            await Task.Delay(-1);
		}

        private IServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandler>()
                .BuildServiceProvider();
        }

        private Task Log(LogMessage log)
		{
			Console.WriteLine(log.ToString());
			return Task.CompletedTask;
		}

		private Task Ready()
		{
            Console.WriteLine($"{_discord.CurrentUser} is connected!");
            return Task.CompletedTask;
		}
    }

    class CommandHandler
    {
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;
        public CommandHandler(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _services = services;
        }

        public async Task Initialize()
        {
            _discord.MessageReceived += MessageReceived;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        public async Task MessageReceived(SocketMessage rawMessage)
        {
            if (!(rawMessage is SocketUserMessage message))
                return;

            if (message.Source != MessageSource.User)
                return;

            if (!string.IsNullOrWhiteSpace(Storage.Axx.AppSettings.WorkingChannel) && message.Channel.Name != Storage.Axx.AppSettings.WorkingChannel)
            {
                if (!(message.Channel is SocketDMChannel))
                    return;
            }

            var argPos = 0;

            string prefix = Config.CmdPrefix;
            if (!message.HasCharPrefix(Config.CmdPrefix[0], ref argPos))
                return;
            
            var context = new SocketCommandContext(_discord, message);
            var result = await _commands.ExecuteAsync(context, argPos, _services);
            
            if (!result.IsSuccess)
            {
                await context.Channel.SendMessageAsync($"Sorry <@{message.Author.Id}>. {result.ErrorReason} Please try \"{Config.CmdPrefix}about\" for more info.");
            }
        }
    }
    
}
