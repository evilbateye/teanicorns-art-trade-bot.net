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
    public abstract class StorageBase
    {
        protected StorageBase _parent = null;
        protected string _path  = "";

        public void SetParent(StorageBase parent) { _parent = parent; }
        public void SetPath(string path) { _path = path; }
        public string GetPath() { return _path; }

        public abstract int Count();
        public abstract void Clear();
        public abstract void Load(string path = null);
        public abstract void Save(string path = null);
    }

    public static class xs
    {
        public const string ENTRIES_PATH = "storage.json";
        public const string HISTORY_PATH = "history.json";
        public const string SETTINGS_PATH = "settings.json";

        public static ApplicationData Entries = new ApplicationData();
        public static ApplicationHistory History = new ApplicationHistory();
        public static ApplicationSettings Settings = new ApplicationSettings();

        static xs()
        {
            Entries.SetPath(ENTRIES_PATH);
            History.SetPath(HISTORY_PATH);
            Settings.SetPath(SETTINGS_PATH);

            Validate(Entries);
            Validate(History);
            Validate(Settings);

            BackupStorage(Entries);
            BackupStorage(History);
            BackupStorage(Settings);
        }

        public static void Initialize(bool bSettingsOnly)
        {
            Console.WriteLine("Axx: Initialize");

            if (Validate(Settings))
            {
                Settings.Load();
                if (Settings != null)
                    Console.WriteLine($"Axx: history-count-{Settings.Count()}");
            }

            if (bSettingsOnly)
                return;
            
            if (Validate(Entries))
            {
                Entries.Load();
                if (Entries != null)
                    Console.WriteLine($"Axx: data-count-{Entries.Count()}");
            }

            if (Validate(History))
            {
                History.Load();
                if (History != null)
                    Console.WriteLine($"Axx: history-count-{History.Count()}");
            }
        }
        private static bool Validate(StorageBase s)
        {
            if (s == null)
                return false;

            if (CreateEmptyIfNeeded(s.GetPath()))
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
        
        public static void BackupStorage(StorageBase s)
        {
            if (s != null)
            {
                string backupName = s.GetPath() + ".bk";
                if (File.Exists(backupName))
                    File.Delete(backupName);
                File.Copy(s.GetPath(), backupName);
            }
        }

        public static void DeleteBackup(StorageBase s)
        {
            if (s != null)
            {
                string backupName = s.GetPath() + ".bk";
                if (File.Exists(backupName))
                    File.Delete(backupName);
            }
        }
        public static void ClearStorage(StorageBase s)
        {
            if (s != null)
            {
                if (s.Count() <= 0)
                    return;
                s.Clear();
                s.Save();
            }
        }

        public static async Task<bool> RestoreStorage(StorageBase s, string url = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                string backupName = s.GetPath() + ".bk";
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
                    await wc.DownloadFileTaskAsync(url, s.GetPath());
                }
                catch (Exception)
                {
                    return false;
                }

                if (!File.Exists(s.GetPath()))
                    return false;

                s.Load();
                s.Save();
            }

            return true;
        }
    }
}
