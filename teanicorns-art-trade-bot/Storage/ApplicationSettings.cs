using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace teanicorns_art_trade_bot.Storage
{
    public class ApplicationSettings : IStorage
    {
        [Flags]
        public enum NofifyFlags
        {
            None = 0,
            Closing = 1,
            FirstNotification = 2,
            SecondNotification = 4,
            ThirdNotification = 8,
        }

        public bool ArtTradeActive = false;
        public string WorkingChannel = "";
        public DateTime TradeStart = DateTime.Now;
        public uint TradeDays = 0;
        public NofifyFlags Notified = NofifyFlags.None;
        public bool ForceTradeEnd = false;

        // public methods
        public void SetForceTradeEnd(bool b)
        {
            ForceTradeEnd = b;
            Save();
        }

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

        public void ActivateTrade(bool bStart, uint? days2start, uint ? days2end, bool? bForce)
        {
            ArtTradeActive = bStart;
            Notified = NofifyFlags.None;

            if (bStart)
                TradeStart = DateTime.Now;

            if (days2start.HasValue)
                TradeStart.AddDays(days2start.Value);

            if (days2end.HasValue)
                TradeDays = days2end.Value;

            if (bForce.HasValue)
                ForceTradeEnd = bForce.Value;

            Save();
        }

        public DateTime GetTradeEnd(int shift = 0)
        {
            return TradeStart.AddDays(TradeDays + shift);
        }

        public void SetNotifyDone(NofifyFlags flag)
        {
            Notified |= flag;
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
