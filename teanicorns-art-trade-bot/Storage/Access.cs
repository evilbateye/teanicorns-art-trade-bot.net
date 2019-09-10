using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Net;

namespace teanicorns_art_trade_bot.Storage
{
    public interface IStorage
    {
        string FileName();
        int Count();
        void Clear();
        void Load(string fileName);
        void Save();
    }
    public class Axx
    {
        public static string AppDataFileName = "storage.json";
        public static ApplicationData AppData = new ApplicationData();
        public static string AppHistoryFileName = "history.json";
        public static ApplicationHistory AppHistory = new ApplicationHistory();
        public static string AppSettingsFileName = "settings.json";
        public static ApplicationSettings AppSettings = new ApplicationSettings();

        static Axx()
        {
            Console.WriteLine("Axx: Constructor");
            Validate(AppData);
            Validate(AppHistory);
        }

        public static void Initialize()
        {
            Console.WriteLine("Axx: Initialize");

            if (Validate(AppData))
            {
                AppData.Load(AppData.FileName());
                if (AppData != null)
                    Console.WriteLine($"Axx: data-count-{AppData.Count()}");
            }

            if (Validate(AppHistory))
            {
                AppHistory.Load(AppHistory.FileName());
                if (AppHistory != null)
                    Console.WriteLine($"Axx: history-count-{AppHistory.Count()}");
            }

            if (Validate(AppSettings))
            {
                AppSettings.Load(AppSettings.FileName());
                if (AppSettings != null)
                    Console.WriteLine($"Axx: history-count-{AppSettings.Count()}");
            }
        }
        private static bool Validate(IStorage s)
        {
            if (s == null)
                return false;

            if (CreateEmptyIfNeeded(s.FileName()))
            {
                if (s.Count() > 0)
                    s.Save();
                return false;
            }

            return true;
        }

        public static bool CreateEmptyIfNeeded(string filename)
        {
            if (!File.Exists(filename))
            {
                File.WriteAllText(filename, "");
                return true;
            }

            return false;
        }
        
        public static void BackupStorage(IStorage s)
        {
            if (s != null)
            {
                string backupName = s.FileName() + ".bk";
                if (File.Exists(backupName))
                    File.Delete(backupName);
                File.Copy(s.FileName(), backupName);
            }
        }
        public static void ClearStorage(IStorage s)
        {
            if (s != null)
            {
                if (s.Count() <= 0)
                    return;
                s.Clear();
                s.Save();
            }
        }

        public static async Task<bool> RestoreStorage(IStorage s, string url = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                string backupName = s.FileName() + ".bk";
                if (!File.Exists(backupName))
                    return false;

                s.Load(backupName);
                s.Save();
                return true;
            }

            using (WebClient wc = new WebClient())
            {
                try
                {
                    await wc.DownloadFileTaskAsync(url, s.FileName());
                }
                catch (Exception)
                {
                    return false;
                }

                if (!File.Exists(s.FileName()))
                    return false;

                s.Load(s.FileName());
                s.Save();
            }

            return true;
        }
    }
}
