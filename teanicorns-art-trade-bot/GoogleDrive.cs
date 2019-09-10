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

namespace teanicorns_art_trade_bot
{
    class GoogleDrive
    {
        private static DriveService _service = null;
        private static string _filePrefix = "teanicorns_";
        private static Dictionary<string, File> _gFiles = new Dictionary<string, File>();

        public static async Task SetupGoogleDrive(DriveService service)
        {
            _service = service;

            File f = await FetchGoogleFile(Storage.Axx.AppDataFileName);
            if (f != null)
                _gFiles.Add(Storage.Axx.AppDataFileName, f);

            f = await FetchGoogleFile(Storage.Axx.AppHistoryFileName);
            if (f != null)
                _gFiles.Add(Storage.Axx.AppHistoryFileName, f);

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += new ElapsedEventHandler(OnUpdateAppDataFile);
            timer.Interval = 300000;
            timer.Enabled = true; 
        }
        private static async void OnUpdateAppDataFile(object source, ElapsedEventArgs e)
        {
            await UploadGoogleFile(Storage.Axx.AppDataFileName);
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
            catch (Exception e)
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
                catch (Exception e)
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
            catch (Exception e)
            {
            }
            
            System.IO.FileStream file = new System.IO.FileStream(fileName, System.IO.FileMode.Truncate, System.IO.FileAccess.Write);

            try
            {
                stream.WriteTo(file);
            }
            catch (Exception e)
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
            catch (Exception e)
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
