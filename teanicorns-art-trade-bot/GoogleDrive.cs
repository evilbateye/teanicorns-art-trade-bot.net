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
            
            File f = await FetchGoogleFile(Storage.Axx.AppDataFileName);
            if (f != null)
                _gFiles.Add(Storage.Axx.AppDataFileName, f);

            f = await FetchGoogleFile(Storage.Axx.AppHistoryFileName);
            if (f != null)
                _gFiles.Add(Storage.Axx.AppHistoryFileName, f);
            
            f = await FetchGoogleFile(Storage.Axx.AppSettingsFileName);
            if (f != null)
                _gFiles.Add(Storage.Axx.AppSettingsFileName, f);
                
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += new ElapsedEventHandler(OnPeriodicUpdate);
            timer.Interval = 600000;
            timer.Enabled = true; 
        }

        private static async void OnPeriodicUpdate(object source, ElapsedEventArgs e)
        {
            if (Storage.Axx.AppSettings.ArtTradeActive)
            {
                SocketTextChannel channel = Utils.FindChannel(_discord, Storage.Axx.AppSettings.WorkingChannel);
                if (channel != null)
                {
                    if (Storage.Axx.AppSettings.TradeDays > 0)
                    {
                        if (DateTime.Now.CompareTo(Storage.Axx.AppSettings.GetTradeEnd(3)) > 0)
                        {
                            string artMissing = Modules.TradeEventModule.GetMissingArtToStr(Storage.Axx.AppData);
                            if (Storage.Axx.AppSettings.ForceTradeEnd || string.IsNullOrWhiteSpace(artMissing))
                            {
                                await Modules.TradeEventModule.StartEntryWeek(_discord);
                            }
                        }
                        else if (DateTime.Now.CompareTo(Storage.Axx.AppSettings.GetTradeEnd()) > 0)
                        {
                            string artMissing = Modules.TradeEventModule.GetMissingArtToStr(Storage.Axx.AppData);

                            if (!Storage.Axx.AppSettings.Notified.HasFlag(Storage.ApplicationSettings.NofifyFlags.Closing))
							{
								Storage.Axx.AppSettings.SetNotifyDone(Storage.ApplicationSettings.NofifyFlags.Closing);
                                
                                if (!string.IsNullOrWhiteSpace(artMissing))
                                    await channel.SendMessageAsync(string.Format(Properties.Resources.GOOGLE_TRADE_ENDING_NOW, Config.CmdPrefix, "reveal art", "about"));
							}

                            if (string.IsNullOrWhiteSpace(artMissing))
                                await Modules.TradeEventModule.StartEntryWeek(_discord);
                        }
                        else if (DateTime.Now.CompareTo(Storage.Axx.AppSettings.GetTradeEnd(-1)) > 0)
                        {
                            if (!Storage.Axx.AppSettings.Notified.HasFlag(Storage.ApplicationSettings.NofifyFlags.ThirdNotification))
                            {
                                Storage.Axx.AppSettings.SetNotifyDone(Storage.ApplicationSettings.NofifyFlags.ThirdNotification);

                                await channel.SendMessageAsync(string.Format(Properties.Resources.GOOGLE_TRADE_ENDING_SOON3));
                            }
                        }
                        else if (DateTime.Now.CompareTo(Storage.Axx.AppSettings.GetTradeEnd(-3)) > 0)
                        {
                            if (!Storage.Axx.AppSettings.Notified.HasFlag(Storage.ApplicationSettings.NofifyFlags.SecondNotification))
                            {
                                Storage.Axx.AppSettings.SetNotifyDone(Storage.ApplicationSettings.NofifyFlags.SecondNotification);

                                await channel.SendMessageAsync(string.Format(Properties.Resources.GOOGLE_TRADE_ENDING_SOON2));
                            }
                        }
                        else if (DateTime.Now.CompareTo(Storage.Axx.AppSettings.GetTradeEnd(-7)) > 0)
                        {
                            if (!Storage.Axx.AppSettings.Notified.HasFlag(Storage.ApplicationSettings.NofifyFlags.FirstNotification))
                            {
                                Storage.Axx.AppSettings.SetNotifyDone(Storage.ApplicationSettings.NofifyFlags.FirstNotification);

                                await channel.SendMessageAsync(string.Format(Properties.Resources.GOOGLE_TRADE_ENDING_SOON1));
                            }
                        }
                    }
                }
            }

            await UploadGoogleFile(Storage.Axx.AppDataFileName);
            await UploadGoogleFile(Storage.Axx.AppSettingsFileName);
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
                await DownloadGoogleFile(f, fileName);
            }

            return f;
        }

        public static async Task DownloadGoogleFile(File f, string fileName)
        {
            var stream = new System.IO.MemoryStream();

            try
            {
                FilesResource.GetRequest req = _service.Files.Get(f.Id);
                await req.DownloadAsync(stream);
            }
            catch (Exception)
            {
            }

            Storage.Axx.CreateEmptyIfNeeded(fileName);
            System.IO.FileStream file = new System.IO.FileStream(fileName, System.IO.FileMode.Truncate, System.IO.FileAccess.Write);

            try
            {
                stream.WriteTo(file);
            }
            catch (Exception)
            {
            }
            finally
            {
                file.Close();
                stream.Close();
            }
        }

        public static async Task UploadGoogleFile(string fileName, string fileId)
        {
            if (!System.IO.File.Exists(fileName))
                return;

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
            }
        }

        public static async Task UploadGoogleFile(string fileName)
        {
            File f;
            if (_gFiles.TryGetValue(fileName, out f))
                await UploadGoogleFile(fileName, f.Id);
        }
    }
}
