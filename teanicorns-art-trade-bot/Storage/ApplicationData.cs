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
    public class ApplicationData : IStorage, ICloneable
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

        // public methods
        public bool ActivateTrade(bool b)
        {
            if (ArtTradeActive != b)
            {
                ArtTradeActive = b;
                Save();
                return true;
            }

            return false;
        }

        public bool SetWorkingChannel(string channel)
        {
            WorkingChannel = channel;
            Save();
            return true;
        }

        public bool SetTheme(string theme)
        {
            Theme = theme;
            Save();
            return true;
        }
        public void Set(UserData data)
        {
            AddOrSetValue(data);
            Save();
        }
        public UserData Get(ulong userId)
        {
            int index;
            return TryGetValue(userId, out index);
        }
        public bool Remove(ulong userId)
        {
            if (TryRemoveValue(userId))
            {
                Save();
                return true;
            }

            return false;
        }
        public void Shuffle()
        {
            Storage = Storage.OrderBy(x => Guid.NewGuid()).ToList();
            Save();
        }
        public bool Next(ulong userId, out UserData nextUser)
        {
            int index;
            nextUser = GetNextValue(userId, out index);
            return nextUser != null;
        }
        public bool Previous(ulong userId, out UserData previousUser)
        {
            int index;
            previousUser = GetPreviousValue(userId, out index);
            return previousUser != null;
        }
        public bool ResetNext(ulong ourId, ulong theirId)
        {
            SetNextValue(ourId, theirId);
            Save();
            return true;
        }
        public List<UserData> GetStorage()
        {
            return Storage;
        }

        // IStorage
        public string FileName() { return Axx.AppDataFileName; }
        public int Count() { return Storage.Count; }
        public void Clear() { Storage.Clear(); }
        public void Load(string fileName)
        {
            string json = File.ReadAllText(fileName);
            var data = JsonConvert.DeserializeObject<ApplicationData>(json);
            if (data != null)
                Axx.AppData = data;
        }
        public void Save()
        {
            if (this == Axx.AppData)
            {
                string json = JsonConvert.SerializeObject(Axx.AppData, Formatting.Indented);
                File.WriteAllText(Axx.AppDataFileName, json);
            }
            else
            {
                Axx.AppHistory.Save();
            }
        }

        // IClonable
        public object Clone()
        {
            ApplicationData clone = (ApplicationData)MemberwiseClone();
            clone.Storage = clone.Storage.Select(x => (UserData)x.Clone()).ToList();
            return clone;
        }
    }
}
