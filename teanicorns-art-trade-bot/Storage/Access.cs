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
    public class xs
    {
        public const string ENTRIES_PATH = "storage.json";
        public const string HISTORY_PATH = "history.json";
        public const string SETTINGS_PATH = "settings.json";

        public static ApplicationData Entries = new ApplicationData();
        public static ApplicationHistory History = new ApplicationHistory();
        public static ApplicationSettings Settings = new ApplicationSettings();

        static xs()
        {
            Validate(Entries);
            Validate(History);
            Validate(Settings);

            BackupStorage(Entries);
            BackupStorage(History);
            BackupStorage(Settings);
        }

        public static void Initialize()
        {
            Console.WriteLine("Axx: Initialize");

            if (Validate(Entries))
            {
                Entries.Load(Entries.FileName());
                if (Entries != null)
                    Console.WriteLine($"Axx: data-count-{Entries.Count()}");
            }

            if (Validate(History))
            {
                History.Load(History.FileName());
                if (History != null)
                    Console.WriteLine($"Axx: history-count-{History.Count()}");
            }

            if (Validate(Settings))
            {
                Settings.Load(Settings.FileName());
                if (Settings != null)
                    Console.WriteLine($"Axx: history-count-{Settings.Count()}");
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
