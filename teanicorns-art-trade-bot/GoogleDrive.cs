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
        private static File _file = null;
        private static string _fileName = "teanicorns_storage.json";

        public static async Task SetupGoogleDrive(DriveService service)
        {
            Console.WriteLine("SetupGoogleDrive: start");

            _service = service;
            FilesResource resource = _service.Files;

            FileList files = null;
            try
            {
                FilesResource.ListRequest req = resource.List();
                req.Q = $"name='{_fileName}'";
                files = await req.ExecuteAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine("SetupGoogleDrive: " + e.ToString());
                return;
            }

            if (files == null)
            {
                Console.WriteLine("SetupGoogleDrive: files == null");
                return;
            }

            if (files.Files.Count <= 0)
            {
                Console.WriteLine("SetupGoogleDrive: files.Files.Count <= 0");

                _file = new File();
                _file.Name = _fileName;
                _file.MimeType = "application/json";

                try
                {
                    FilesResource.CreateRequest create = resource.Create(_file);
                    await create.ExecuteAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine("SetupGoogleDrive: " + e.ToString());
                }
            }
            else
            {
                _file = files.Files[0];
                await DownloadGoogleFile();
            }

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            timer.Interval = 300000;
            timer.Enabled = true; 
        }

        private static async void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            await UploadGoogleFile();

        }

        public static async Task DownloadGoogleFile()
        {
            Console.WriteLine("DownloadGoogleFile: start");

            var stream = new System.IO.MemoryStream();

            try
            {
                FilesResource.GetRequest req = _service.Files.Get(_file.Id);
                await req.DownloadAsync(stream);
            }
            catch (Exception e)
            {
                Console.WriteLine("DownloadGoogleFile: " + e.ToString());
            }
            
            System.IO.FileStream file = new System.IO.FileStream(PersistentStorage.storageFileName, System.IO.FileMode.Truncate, System.IO.FileAccess.Write);

            try
            {
                stream.WriteTo(file);
            }
            catch (Exception e)
            {
                Console.WriteLine("DownloadGoogleFile: " + e.ToString());
            }
            finally
            {
                file.Close();
                stream.Close();
                Console.WriteLine("DownloadGoogleFile: stream.Length=", stream.Length);
            }
        }

        public static async Task UploadGoogleFile()
        {
            Console.WriteLine("UploadGoogleFile: start");

            if (!System.IO.File.Exists(PersistentStorage.storageFileName))
                return;

            byte[] byteArray = System.IO.File.ReadAllBytes(PersistentStorage.storageFileName);
            System.IO.MemoryStream stream = new System.IO.MemoryStream(byteArray);

            Console.WriteLine("UploadGoogleFile: byteArray.Length=", byteArray.Length);

            try
            {
                File body = new File();
                FilesResource.UpdateMediaUpload req = _service.Files.Update(body, _file.Id, stream, "application/json");
                var progress = await req.UploadAsync();
                File response = req.ResponseBody;
            }
            catch (Exception e)
            {
                Console.WriteLine("UploadGoogleFile: " + e.ToString());
            }
        }

    }
}
