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
        public DateTime TradeStart = DateTime.Now;
        public uint TradeDays = 0;
        public bool NotifyPending = false;

        // public methods
        public bool SetWorkingChannel(string channel)
        {
            WorkingChannel = channel;
            Save();
            return true;
        }

        public void SetTradeStartNow()
        {
            TradeStart = DateTime.Now;
            Save();
        }

        public void SetTradeEnd(uint days)
        {
            TradeDays = days;
            Save(); 
        }

        public void ActivateTrade(bool bStart, uint days)
        {
            ArtTradeActive = bStart;
            TradeDays = days;
            NotifyPending = bStart;
            if (bStart)
                TradeStart = DateTime.Now;
            Save();
        }

        public DateTime GetTradeEnd(int shift = 0)
        {
            return TradeStart.AddDays(TradeDays + shift);
        }

        public void SetNotifyPending(bool bPending)
        {
            NotifyPending = bPending;
            Save();
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
