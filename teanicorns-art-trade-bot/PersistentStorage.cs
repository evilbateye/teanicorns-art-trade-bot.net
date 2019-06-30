using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Net;

namespace teanicorns_art_trade_bot
{
    public class PersistentStorage
    {
        public class UserData
        {
            public ulong UserId;
            public string UserName;
            public string ReferenceUrl;
            public string ReferenceDescription;
            public string NickName;
            public string ArtUrl;
        }

        public class ApplicationData
        {
            public bool ArtTradeActive = false;
            public string WorkingChannel = "";
            public string Theme = "";
            public List<UserData> Storage = new List<UserData>();

            public UserData TryGetValue(ulong userId, out int index)
            {
                index = Storage.FindIndex(x => x.UserId == userId);
                if (index != -1)
                {
                    return Storage[index];
                }

                return null;
            }

            public void AddOrSetValue(UserData data)
            {
                int index = Storage.FindIndex(x => x.UserId == data.UserId);
                if (index != -1)
                {
                    if (!string.IsNullOrWhiteSpace(data.ReferenceDescription))
                        Storage[index].ReferenceDescription = data.ReferenceDescription;
                    if (!string.IsNullOrWhiteSpace(data.ReferenceUrl))
                        Storage[index].ReferenceUrl = data.ReferenceUrl;
                    if (!string.IsNullOrWhiteSpace(data.NickName))
                        Storage[index].NickName = data.NickName;
                    if (!string.IsNullOrWhiteSpace(data.ArtUrl))
                        Storage[index].ArtUrl = data.ArtUrl;
                }
                else
                    Storage.Add(data);
            }

            public bool TryRemoveValue(ulong userId)
            {
                int index = Storage.FindIndex(x => x.UserId == userId);
                if (index != -1)
                {
                    Storage.RemoveAt(index);
                    return true;
                }

                return false;
            }

            public UserData GetNextValue(ulong userId, out int index)
            {
                index = Storage.FindIndex(x => x.UserId == userId);
                if (index != -1)
                {
                    ++index;
                    index = (index == Storage.Count ? 0 : index);
                    return Storage[index];
                }

                return null;
            }

            public UserData GetPreviousValue(ulong userId, out int index)
            {
                index = Storage.FindIndex(x => x.UserId == userId);
                if (index != -1)
                {
                    --index;
                    index = (index == -1 ? Storage.Count - 1 : index);
                    return Storage[index];
                }

                return null;
            }

            public bool SetNextValue(ulong ourId, ulong theirId)
            {
                int prevIndex;
                UserData prevUser = GetPreviousValue(theirId, out prevIndex);
                if (prevUser == null)
                    return false;

                int ourIndex;
                UserData ourUser = TryGetValue(ourId, out ourIndex);
                if (ourUser == null)
                    return false;

                Storage[prevIndex] = ourUser;
                Storage[ourIndex] = prevUser;
                return true;
            }
        }

        public static string storageFileName = "storage.json";
        public static ApplicationData AppData = new ApplicationData();
        static PersistentStorage()
        {
            Console.WriteLine("PersistentStorage: constructor");
            Validate(storageFileName);
        }

        public static void Initialize()
        {
            Console.WriteLine("PersistentStorage: Initialize");

            if (!Validate(storageFileName))
                return;

            string json = File.ReadAllText(storageFileName);

            var loaded = JsonConvert.DeserializeObject<ApplicationData>(json);
            if (loaded != null)
                AppData = loaded;

            Console.WriteLine("PersistentStorage: " + AppData.Storage.Count);
        }
        private static bool Validate(string filePath)
        {
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "");
                if (AppData.Storage.Count > 0)
                    Save();
                return false;
            }

            return true;
        }
                
        public static bool ActivateTrade(bool b)
        {
            if (AppData != null)
            {
                if (AppData.ArtTradeActive != b)
                {
                    AppData.ArtTradeActive = b;
                    Save();
                    return true;
                }
            }

            return false;
        }

        public static bool SetWorkingChannel(string channel)
        {
            if (AppData != null)
            {
                AppData.WorkingChannel = channel;
                Save();
                return true;
            }

            return false;
        }

        public static bool SetTheme(string theme)
        {
            if (AppData != null)
            {
                AppData.Theme = theme;
                Save();
                return true;
            }

            return false;
        }

        public static void BackupStorage()
        {
            if (AppData != null)
            {
                string backupName = storageFileName + ".bk";
                if (File.Exists(backupName))
                    File.Delete(backupName);
                File.Copy(storageFileName, backupName);
            }
        }
        public static void ClearStorage()
        {
            if (AppData != null)
            {
                if (AppData.Storage.Count <= 0)
                    return;
                AppData.Storage.Clear();
                Save();
            }
        }

        public static bool RestoreStorage(string fileName = null)
        {
            string backupName = fileName;
            if (string.IsNullOrWhiteSpace(fileName))
                backupName = storageFileName + ".bk";

            if (!File.Exists(backupName))
                return false;

            string json = File.ReadAllText(backupName);
            AppData = JsonConvert.DeserializeObject<ApplicationData>(json);
            Save();
            return true;
        }

        public static async Task<bool> RestoreStorageFromUrl(string url = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            using (WebClient wc = new WebClient())
            {
                try
                {
                    await wc.DownloadFileTaskAsync(url, storageFileName);
                }
                catch (Exception e)
                {
                    return false;
                }

                if (!File.Exists(storageFileName))
                    return false;

                string json = File.ReadAllText(storageFileName);
                AppData = JsonConvert.DeserializeObject<ApplicationData>(json);
            }

            return true;
        }

        public static void Set(UserData data)
        {
            if (AppData != null)
            {
                AppData.AddOrSetValue(data);
                Save();
            }
        }

        public static UserData Get(ulong userId)
        {
            int index;
            if (AppData != null)
                return AppData.TryGetValue(userId, out index);
            return null;
        }

        public static bool Remove(ulong userId)
        {
            if (AppData != null)
            {
                if (AppData.TryRemoveValue(userId))
                {
                    Save();
                    return true;
                }
            }

            return false;
        }

        public static void Save()
        {
            if (AppData != null)
            {
                string json = JsonConvert.SerializeObject(AppData, Formatting.Indented);
                File.WriteAllText(storageFileName, json);
            }
        }

        public static void Shuffle()
        {
            if (AppData != null)
            {
                AppData.Storage = AppData.Storage.OrderBy(x => Guid.NewGuid()).ToList();
                Save();
            }
        }

        public static bool Next(ulong userId, out UserData nextUser)
        {
            int index;
            nextUser = null;
            if (AppData != null)
                nextUser = AppData.GetNextValue(userId, out index);
            return nextUser != null;
        }

        public static bool Previous(ulong userId, out UserData previousUser)
        {
            int index;
            previousUser = null;
            if (AppData != null)
                previousUser = AppData.GetPreviousValue(userId, out index);
            return previousUser != null;
        }

        public static bool ResetNext(ulong ourId, ulong theirId)
        {
            if (AppData != null)
            {
                AppData.SetNextValue(ourId, theirId);
                Save();
                return true;
            }
            return false;
        }

        public static List<UserData> GetStorage()
        {
            if (AppData != null)
                return AppData.Storage;
            return null;
        }
    }
}
