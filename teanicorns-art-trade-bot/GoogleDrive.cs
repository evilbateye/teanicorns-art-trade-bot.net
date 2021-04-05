using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;

using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace teanicorns_art_trade_bot
{
    public class GoogleDriveHandler
    {
        private static DriveService _service = null;
        private static string _filePrefix = "teanicorns_";
        private static Dictionary<string, File> _gFiles = new Dictionary<string, File>();

        private static DiscordSocketClient _discord;
        public GoogleDriveHandler(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
        }

        public static async Task SetupGoogleDrive(DriveService service)
        {
            _service = service;
            
            File f = await FetchGoogleFile(Storage.xs.ENTRIES_PATH);
            if (f != null)
                _gFiles.Add(Storage.xs.ENTRIES_PATH, f);

            f = await FetchGoogleFile(Storage.xs.HISTORY_PATH);
            if (f != null)
                _gFiles.Add(Storage.xs.HISTORY_PATH, f);
            
            f = await FetchGoogleFile(Storage.xs.SETTINGS_PATH);
            if (f != null)
                _gFiles.Add(Storage.xs.SETTINGS_PATH, f);
                
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += new ElapsedEventHandler(OnPeriodicUpdate);
            timer.Interval = 600000;
            timer.Enabled = true;

            await UploadGoogleFile(Storage.xs.ENTRIES_PATH);
            await UploadGoogleFile(Storage.xs.HISTORY_PATH);
            await UploadGoogleFile(Storage.xs.SETTINGS_PATH);
        }

        private static async void OnPeriodicUpdate(object source, ElapsedEventArgs e)
        {
            if (!Storage.xs.Settings.IsGDriveOn())
                return;

            if (Storage.xs.Settings.IsTradeMonthActive())
            {
                SocketTextChannel channel = Utils.FindChannel(_discord, Storage.xs.Settings.GetWorkingChannel());
                if (channel != null && Storage.xs.Settings.GetTradeDays() > 0)
                {
                    if (DateTime.Now.CompareTo(Storage.xs.Settings.GetTradeEnd(3)) > 0)
                    {
                        string artMissing = Utils.GetMissingArtToStr(Storage.xs.Entries);
                        if (Storage.xs.Settings.IsForceTradeOn() || string.IsNullOrWhiteSpace(artMissing))
                        {
                            await Modules.TradeEventModule.StartEntryWeek(_discord);
                        }
                    }
                    else if (DateTime.Now.CompareTo(Storage.xs.Settings.GetTradeEnd()) > 0)
                    {
                        string artMissing = Utils.GetMissingArtToStr(Storage.xs.Entries);

                        if (!Storage.xs.Settings.HasNotifyFlag(Storage.ApplicationSettings.NofifyFlags.Closing))
                        {
                            Storage.xs.Settings.SetNotifyDone(Storage.ApplicationSettings.NofifyFlags.Closing);

                            if (!string.IsNullOrWhiteSpace(artMissing))
                                await channel.SendMessageAsync(embed: Utils.EmbedMessage(_discord, string.Format(Properties.Resources.GOOGLE_TRADE_ENDING_NOW, Config.CmdPrefix, "reveal art", "about"), Utils.Emotion.negative));

                            foreach (var entry in Storage.xs.Entries.GetStorage())
                                await Utils.SendPartnerResponse(_discord, entry, Storage.xs.Entries.GetTheme(), true /*notify channel*/);
                        }

                        if (string.IsNullOrWhiteSpace(artMissing))
                            await Modules.TradeEventModule.StartEntryWeek(_discord);
                    }
                    else if (DateTime.Now.CompareTo(Storage.xs.Settings.GetTradeEnd(-1)) > 0)
                    {
                        if (!Storage.xs.Settings.HasNotifyFlag(Storage.ApplicationSettings.NofifyFlags.ThirdNotification))
                        {
                            Storage.xs.Settings.SetNotifyDone(Storage.ApplicationSettings.NofifyFlags.ThirdNotification);

                            string message = string.Format(Properties.Resources.GOOGLE_TRADE_ENDING_SOON, "`tomorrow`");

                            await channel.SendMessageAsync(embed: Utils.EmbedMessage(_discord, message, Utils.Emotion.positive));

                            await Utils.NotifySubscribers(_discord, "the art trade will be ending `tomorrow`");
                        }
                    }
                    else if (DateTime.Now.CompareTo(Storage.xs.Settings.GetTradeEnd(-3)) > 0)
                    {
                        if (!Storage.xs.Settings.HasNotifyFlag(Storage.ApplicationSettings.NofifyFlags.SecondNotification))
                        {
                            Storage.xs.Settings.SetNotifyDone(Storage.ApplicationSettings.NofifyFlags.SecondNotification);

                            string message = string.Format(Properties.Resources.GOOGLE_TRADE_ENDING_SOON, "in `3` days");

                            await channel.SendMessageAsync(embed: Utils.EmbedMessage(_discord, message, Utils.Emotion.positive));

                            await Utils.NotifySubscribers(_discord, "the art trade will be ending in `3` days");
                        }
                    }
                    else if (DateTime.Now.CompareTo(Storage.xs.Settings.GetTradeEnd(-7)) > 0)
                    {
                        if (!Storage.xs.Settings.HasNotifyFlag(Storage.ApplicationSettings.NofifyFlags.FirstNotification))
                        {
                            Storage.xs.Settings.SetNotifyDone(Storage.ApplicationSettings.NofifyFlags.FirstNotification);

                            string message = string.Format(Properties.Resources.GOOGLE_TRADE_ENDING_SOON, "in `7` days");

                            await channel.SendMessageAsync(embed: Utils.EmbedMessage(_discord, message, Utils.Emotion.positive));

                            await Utils.NotifySubscribers(_discord, "the art trade will be ending in `7` days");
                        }
                    }
                }
            }

            await UploadGoogleFile(Storage.xs.ENTRIES_PATH);
            await UploadGoogleFile(Storage.xs.SETTINGS_PATH);
        }

        public static async Task<File> FetchGoogleFile(string fileName)
        {
            File f = null;
            FilesResource resource = _service.Files;
            FileList files = null;
            try
            {
                FilesResource.ListRequest req = resource.List();
                req.Q = $"name='{_filePrefix + fileName}'";
                files = await req.ExecuteAsync();
            }
            catch (Exception)
            {
                return null;
            }

            if (files == null)
            {
                return null;
            }

            if (files.Files.Count <= 0)
            {
                f = new File();
                f.Name = _filePrefix + fileName;
                f.MimeType = "application/json";

                try
                {
                    FilesResource.CreateRequest create = resource.Create(f);
                    await create.ExecuteAsync();
                }
                catch (Exception)
                {
                }
            }
            else
            {
                f = files.Files[0];
            }

            await DownloadGoogleFile(f, fileName);
            return f;
        }

        public static async Task<bool> DownloadGoogleFile(File f, string fileName)
        {
            if (!Storage.xs.Settings.IsGDriveOn())
                return false;

            var stream = new System.IO.MemoryStream();

            try
            {
                FilesResource.GetRequest req = _service.Files.Get(f.Id);
                await req.DownloadAsync(stream);
            }
            catch (Exception)
            {
                return false;
            }

            Storage.xs.CreateEmptyIfNeeded(fileName);
            System.IO.FileStream file = new System.IO.FileStream(fileName, System.IO.FileMode.Truncate, System.IO.FileAccess.Write);

            try
            {
                stream.WriteTo(file);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                file.Close();
                stream.Close();
            }

            return true;
        }

        public static async Task<bool> UploadGoogleFile(string fileName, string fileId)
        {
            if (!System.IO.File.Exists(fileName) || !Storage.xs.Settings.IsGDriveOn())
                return false;

            byte[] byteArray = System.IO.File.ReadAllBytes(fileName);
            System.IO.MemoryStream stream = new System.IO.MemoryStream(byteArray);

            try
            {
                File body = new File();
                FilesResource.UpdateMediaUpload req = _service.Files.Update(body, fileId, stream, "application/json");
                var progress = await req.UploadAsync();
                File response = req.ResponseBody;
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public static async Task<bool> UploadGoogleFile(string fileName)
        {
            File f;
            if (!_gFiles.TryGetValue(fileName, out f))
                return false;
            return await UploadGoogleFile(fileName, f.Id);
        }
    }
}
