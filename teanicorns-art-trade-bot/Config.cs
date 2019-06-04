﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace teanicorns_art_trade_bot
{
    class Config
    {
        private static BotConfig _bot = new BotConfig();
        public static string DiscordToken { get => _bot.DiscordToken; }
		public static string CmdPrefix { get => _bot.CmdPrefix; }
        public static ICredential GoogleCred { get => _bot.GoogleCred; }
        static Config()
        {
            string localBotDiscordToken = Environment.GetEnvironmentVariable("ATB_TOKEN");
            if (!string.IsNullOrWhiteSpace(localBotDiscordToken))
                _bot.DiscordToken = localBotDiscordToken;

            string localBotPrefix = Environment.GetEnvironmentVariable("ATB_PREFIX");
            if (!string.IsNullOrWhiteSpace(localBotPrefix))
                _bot.CmdPrefix = localBotPrefix;

            string[] scopes = new string[] { DriveService.Scope.Drive,  // view and manage your files and documents
                                             DriveService.Scope.DriveAppdata,  // view and manage its own configuration data
                                             DriveService.Scope.DriveFile,   // view and manage files created by this app
                                             DriveService.Scope.DriveMetadataReadonly,   // view metadata for files
                                             DriveService.Scope.DriveReadonly,   // view files and documents on your drive
                                             DriveService.Scope.DriveScripts }; // modify your app scripts

            string localGAccountEmail = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_EMAIL");
            string localGAccountPrivateKey = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_PRIVATE_KEY");
            localGAccountPrivateKey = localGAccountPrivateKey.Replace("\\n", "\n");

            if (!string.IsNullOrWhiteSpace(localGAccountEmail) && !string.IsNullOrWhiteSpace(localGAccountPrivateKey))
            {
                _bot.GoogleCred = new ServiceAccountCredential(
                    new ServiceAccountCredential.Initializer(localGAccountEmail)
                    {
                        Scopes = scopes
                    }.FromPrivateKey(localGAccountPrivateKey));
            }
        }

        public class BotConfig
        {
            public string DiscordToken;
            public string CmdPrefix = ".";
            public ICredential GoogleCred = null;
        }
    }
}