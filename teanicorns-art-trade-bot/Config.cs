using System;
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
        
        public static string[] GetEmotions(Utils.Emotion e)
        {
            switch (e)
            {
                case Utils.Emotion.positive:
                    return _bot.positiveEmotions;

                case Utils.Emotion.neutral:
                    return _bot.neutralEmotions;

                case Utils.Emotion.negative:
                    return _bot.negativeEmotions;

                default:
                    return null;
            }
        }
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

            if (!string.IsNullOrWhiteSpace(localGAccountEmail) && !string.IsNullOrWhiteSpace(localGAccountPrivateKey))
            {
				localGAccountPrivateKey = localGAccountPrivateKey.Replace("\\n", "\n");

				_bot.GoogleCred = new ServiceAccountCredential(
                    new ServiceAccountCredential.Initializer(localGAccountEmail)
                    {
                        Scopes = scopes
                    }.FromPrivateKey(localGAccountPrivateKey));
            }

            string localPosEmotions = Environment.GetEnvironmentVariable("POSITIVE_EMOTIONS");
            if (!string.IsNullOrWhiteSpace(localPosEmotions))
                _bot.positiveEmotions = localPosEmotions.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            
            string localNeuEmotions = Environment.GetEnvironmentVariable("NEUTRAL_EMOTIONS");
            if (!string.IsNullOrWhiteSpace(localNeuEmotions))
                _bot.neutralEmotions = localNeuEmotions.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

            string localNegEmotions = Environment.GetEnvironmentVariable("NEGATIVE_EMOTIONS");
            if (!string.IsNullOrWhiteSpace(localNegEmotions))
                _bot.negativeEmotions = localNegEmotions.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
        }

        public class BotConfig
        {
            public string DiscordToken;
            public string CmdPrefix = ".";
            public ICredential GoogleCred = null;
            public string[] positiveEmotions = null;
            public string[] neutralEmotions = null;
            public string[] negativeEmotions = null;
        }
    }
}
