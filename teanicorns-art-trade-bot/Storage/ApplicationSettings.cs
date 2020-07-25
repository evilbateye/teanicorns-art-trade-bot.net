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

        public enum TradeSegment
        {
            EntryWeek = 0,
            ThemesPoll = 1,
            TradeMonth = 2
        }

        public TradeSegment ArtTradeActive = TradeSegment.EntryWeek;
        public string WorkingChannel = "general";
        public DateTime TradeStart = DateTime.Now;
        public double TradeDays = 0.0;
        public NofifyFlags Notified = NofifyFlags.None;
        public bool ForceTradeEnd = false;
        public ulong ThemePollID = 0;
        public List<ulong> Subscribers = new List<ulong>();

        public List<ulong> GetSubs()
        {
            return Subscribers;
        }

        public ulong GetThemePollID()
        {
            return ThemePollID;
        }

        public bool IsForceTradeOn()
        {
            return ForceTradeEnd;
        }

        public NofifyFlags GetNotifyFlags()
        {
            return Notified;
        }

        public bool HasNotifyFlag(NofifyFlags flag)
        {
            return Notified.HasFlag(flag);
        }

        public double GetTradeDays()
        {
            return TradeDays;
        }

        public string GetWorkingChannel()
        {
            return WorkingChannel;
        }

        public bool IsTradeMonthActive()
        {
            return ArtTradeActive == TradeSegment.TradeMonth;
        }

        public bool IsEntryWeekActive()
        {
            return ArtTradeActive == TradeSegment.EntryWeek;
        }

        public bool IsThemePollActive()
        {
            return ArtTradeActive == TradeSegment.ThemesPoll;
        }

        public TradeSegment GetActiveTradeSegment()
        {
            return ArtTradeActive;
        }

        public bool ChangeSubscription(ulong userID, bool ? bOnOff)
        {
            if (bOnOff.HasValue)
            {
                if (bOnOff.Value)
                {
                    if (Subscribers.Contains(userID))
                        return false;
                    Subscribers.Add(userID);
                }
                else
                {
                    if (!Subscribers.Remove(userID))
                        return false;
                }
            }
            else if (Subscribers.Contains(userID))
                Subscribers.Remove(userID);
            else
                Subscribers.Add(userID);

            Save();
            return true;
        }

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

        public void ActivateTrade(TradeSegment? seg, double? days2start, double? days2end, bool? bForce)
        {
            if (seg.HasValue)
                ArtTradeActive = seg.Value;

            Notified = NofifyFlags.None;

            switch (ArtTradeActive)
            {
                case TradeSegment.TradeMonth:
                case TradeSegment.ThemesPoll:
                    TradeStart = DateTime.Now;
                    break;
                case TradeSegment.EntryWeek:
                    ThemePollID = 0;
                    break;
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
