﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace teanicorns_art_trade_bot.Storage
{
    public class ApplicationSettings : StorageBase
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

        public const string DEFAULT_WORK_CHANNEL = "general";
        [JsonProperty("ArtTradeActive")] private TradeSegment _artTradeActive = TradeSegment.EntryWeek;
        [JsonProperty("WorkingChannel")] private string _workingChannel = DEFAULT_WORK_CHANNEL;
        [JsonProperty("TradeStart")] private DateTime _tradeStart = DateTime.Now;
        [JsonProperty("TradeDays")] private double _tradeDays = 0.0;
        [JsonProperty("Notified")] private NofifyFlags _notified = NofifyFlags.None;
        [JsonProperty("ForceTradeEnd")] private bool _forceTradeEnd = false;
        [JsonProperty("ThemePollID")] private ulong _themePollID = 0;
        [JsonProperty("Subscribers")] private List<ulong> _subscribers = new List<ulong>();

        public List<ulong> GetSubscribers()
        {
            return _subscribers;
        }

        public ulong GetThemePollID()
        {
            return _themePollID;
        }

        public bool IsForceTradeOn()
        {
            return _forceTradeEnd;
        }

        public NofifyFlags GetNotifyFlags()
        {
            return _notified;
        }

        public bool HasNotifyFlag(NofifyFlags flag)
        {
            return _notified.HasFlag(flag);
        }

        public double GetTradeDays()
        {
            return _tradeDays;
        }

        public string GetWorkingChannel()
        {
            return string.IsNullOrWhiteSpace(_workingChannel) ? DEFAULT_WORK_CHANNEL : _workingChannel;
        }

        public bool IsTradeMonthActive()
        {
            return _artTradeActive == TradeSegment.TradeMonth;
        }

        public bool IsEntryWeekActive()
        {
            return _artTradeActive == TradeSegment.EntryWeek;
        }

        public bool IsThemePollActive()
        {
            return _artTradeActive == TradeSegment.ThemesPoll;
        }

        public TradeSegment GetActiveTradeSegment()
        {
            return _artTradeActive;
        }

        public bool ChangeSubscription(ulong userID, bool ? bOnOff)
        {
            if (bOnOff.HasValue)
            {
                if (bOnOff.Value)
                {
                    if (_subscribers.Contains(userID))
                        return false;
                    _subscribers.Add(userID);
                }
                else
                {
                    if (!_subscribers.Remove(userID))
                        return false;
                }
            }
            else if (_subscribers.Contains(userID))
                _subscribers.Remove(userID);
            else
                _subscribers.Add(userID);

            Save();
            return true;
        }

        public void SetThemePollID(ulong id)
        {
            _themePollID = id;
            Save();
        }

        // public methods
        public void SetForceTradeEnd(bool b)
        {
            _forceTradeEnd = b;
            Save();
        }

        public bool SetWorkingChannel(string channel)
        {
            _workingChannel = channel;
            Save();
            return true;
        }

        public void SetTradeStartNow()
        {
            _tradeStart = DateTime.Now;
            Save();
        }

        public void SetTradeEnd(double days)
        {
            _tradeDays = days;
            Save(); 
        }

        public void ActivateTrade(TradeSegment? seg, double? days2start, double? days2end, bool? bForce)
        {
            if (seg.HasValue)
                _artTradeActive = seg.Value;

            _notified = NofifyFlags.None;

            switch (_artTradeActive)
            {
                case TradeSegment.TradeMonth:
                case TradeSegment.ThemesPoll:
                    _tradeStart = DateTime.Now;
                    break;
                case TradeSegment.EntryWeek:
                    _themePollID = 0;
                    break;
            }

            if (days2start.HasValue)
                _tradeStart = _tradeStart.AddDays(days2start.Value);

            if (days2end.HasValue)
                _tradeDays = days2end.Value;

            if (bForce.HasValue)
                _forceTradeEnd = bForce.Value;

            Save();
        }

        public DateTime GetTradeEnd(double shift = 0)
        {
            return _tradeStart.AddDays(_tradeDays + shift);
        }

        public DateTime GetTradeStart(double shift = 0)
        {
            return _tradeStart.AddDays(shift);
        }

        public void SetNotifyDone(NofifyFlags flag)
        {
            _notified |= flag;
            Save();
        }

        // StorageBase methods
        public override int Count() { return 1; }
        public override void Clear() { }
        public override StorageBase Load(string path = null)
        {
            string json = File.ReadAllText(path == null ? _path : path);
            var data = JsonConvert.DeserializeObject<ApplicationSettings>(json);
            if (data != null)
                data.SetPath(_path);
            return data;
        }
        public override void Save(string path = null)
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path == null ? _path : path, json);
        }
    }
}
