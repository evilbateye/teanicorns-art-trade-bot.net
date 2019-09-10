using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace teanicorns_art_trade_bot.Storage
{
    public class ApplicationSettings : IStorage
    {
        public bool ArtTradeActive = false;
        public string WorkingChannel = "";

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

        // IStorage methods
        public string FileName() { return Axx.AppSettingsFileName; }
        public int Count() { return 1; }
        public void Clear() {}
        public void Load(string fileName)
        {
            string json = File.ReadAllText(fileName);
            var data = JsonConvert.DeserializeObject<ApplicationSettings>(json);
            if (data != null)
                Axx.AppSettings = data;
        }
        public void Save()
        {
            string json = JsonConvert.SerializeObject(Axx.AppSettings, Formatting.Indented);
            File.WriteAllText(Axx.AppSettingsFileName, json);
        }
    }
}
