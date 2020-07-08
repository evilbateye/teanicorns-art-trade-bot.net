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
            ThemePollNotification = 16,
        }

        public bool ArtTradeActive = false;
        public string WorkingChannel = "";
        public DateTime TradeStart = DateTime.Now;
        public double TradeDays = 0.0;
        public NofifyFlags Notified = NofifyFlags.None;
        public bool ForceTradeEnd = false;
        public ulong ThemePollID = 0;

        public void SetThemePollID(ulong id)
        {
            ThemePollID = id;
            Save();
        }

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

        public void SetTradeEnd(double days)
        {
            TradeDays = days;
            Save(); 
        }

        public void ActivateTrade(bool? bStart, double? days2start, double? days2end, bool? bForce)
        {
            if (bStart.HasValue)
                ArtTradeActive = bStart.Value;

            Notified = NofifyFlags.None;

            if (ArtTradeActive)
            {
                TradeStart = DateTime.Now;
            }
            else
            {
                ThemePollID = 0;
            }

            if (days2start.HasValue)
                TradeStart = TradeStart.AddDays(days2start.Value);

            if (days2end.HasValue)
                TradeDays = days2end.Value;

            if (bForce.HasValue)
                ForceTradeEnd = bForce.Value;

            Save();
        }

        public DateTime GetTradeEnd(double shift = 0)
        {
            return TradeStart.AddDays(TradeDays + shift);
        }

        public DateTime GetTradeStart(double shift = 0)
        {
            return TradeStart.AddDays(shift);
        }

        public void SetNotifyDone(NofifyFlags flag)
        {
            Notified |= flag;
            Save();
        }

        // IStorage methods
        public string FileName() { return Axx.AppSettingsFileName; }
        public int Count() { return 1; }
        public void Clear()
        {
        }
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
