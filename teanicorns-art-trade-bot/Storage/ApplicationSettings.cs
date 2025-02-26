﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection.Metadata.Ecma335;

namespace teanicorns_art_trade_bot.Storage
{
    public class ArtTheme
    {
        [JsonProperty("Theme")] public string Theme = "";
        [JsonProperty("EmojiCode")] public string EmojiCode = "";
    }

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
            TradeMonth = 1
        }

        public enum MsgIDType
        {
            ThemePoll = 0,
            Help = 1,
            NaughtyList = 2
        }

        public const string DEFAULT_WORK_CHANNEL = "general";
        public const int MAX_THEMES_COUNT = 10;
        [JsonProperty("ArtTradeActive")] private TradeSegment _artTradeActive = TradeSegment.EntryWeek;
        [JsonProperty("WorkingChannel")] private string _workingChannel = DEFAULT_WORK_CHANNEL;
        [JsonProperty("TradeStart")] private DateTime _tradeStart = DateTime.Now;
        [JsonProperty("TradeDays")] private double _tradeDays = 0.0;
        [JsonProperty("Notified")] private NofifyFlags _notified = NofifyFlags.None;
        [JsonProperty("ForceTradeEnd")] private bool _forceTradeEnd = false;
        [JsonProperty("MsgIDs")] private ulong[] _msgIDs = new ulong[3] { 0, 0, 0 };
        [JsonProperty("Subscribers")] private List<ulong> _subscribers = new List<ulong>();
        [JsonProperty("ThemePool")] private Dictionary<ulong, List<ArtTheme>> _themePool = new Dictionary<ulong, List<ArtTheme>>();
        [JsonProperty("GDriveOn")] private bool _gDriveOn = true;

        public ulong[] GetMsgIDs()
        {
            return _msgIDs;
        }

        public ulong GetHelpMessageId()
        {
            return _msgIDs[(int)MsgIDType.Help];
        }

        public void SetHelpMessageId(ulong id)
        {
            _msgIDs[(int)MsgIDType.Help] = id;
            Save();
        }

        public ulong GetNaughtyListMessageId()
        {
            return _msgIDs[(int)MsgIDType.NaughtyList];
        }

        public void SetNaughtyListMessageId(ulong id)
        {
            _msgIDs[(int)MsgIDType.NaughtyList] = id;
            Save();
        }

        public bool IsThemePoolMaxed()
        {
            return GetThemePoolTotal() == MAX_THEMES_COUNT;
        }

        public int GetThemePoolTotal()
        {
            return _themePool.SelectMany(x => x.Value).Count();
        }

        public Dictionary<ulong, List<ArtTheme>> GetThemePool()
        {
            return _themePool;
        }

        public bool GetThemePool(ulong userID, out List<ArtTheme> themes)
        {
            return _themePool.TryGetValue(userID, out themes);
        }
        public (ulong, ArtTheme) FindArtThemeByTheme(string theme)
        {
            foreach (KeyValuePair<ulong, List<ArtTheme>> pair in _themePool)
            {
                var artTheme = pair.Value.FirstOrDefault(x => x.Theme == theme);
                if (artTheme != default)
                    return (pair.Key, artTheme);
            }

            return default;
        }

        public (ulong, ArtTheme) FindArtThemeByEmoji(string emoji)
        {
            foreach(KeyValuePair<ulong, List<ArtTheme>> pair in _themePool)
            {
                var artTheme = pair.Value.FirstOrDefault(x => x.EmojiCode == emoji);
                if (artTheme != default)
                    return (pair.Key, artTheme);
            }

            return default;
        }

        public bool AddThemeToPool(ulong userID, string theme)
        {
            if (IsThemePoolMaxed())
                return false;

            theme = theme.ToLower().Trim();

            string customEmoji = "";
            Match emojiMatch = Utils.EmojiPattern.Match(theme);
            if (emojiMatch.Success)
            {
                theme = string.Join(' ', theme.Split(emojiMatch.Value).Select(x => x.Trim())).Trim();
                customEmoji = emojiMatch.Value;
            }
            else
            {
                foreach (string defaultEmoji in Utils.EmojiCodes)
                {
                    if (FindArtThemeByEmoji(defaultEmoji) == default)
                        customEmoji = defaultEmoji;
                }
            }

            List<ArtTheme> themes;
            if (_themePool.TryGetValue(userID, out themes))
            {
                var foundArtTheme = themes.FirstOrDefault(x => x.Theme == theme);
                if (foundArtTheme != default)
                    return false;
                themes.Add(new ArtTheme { Theme = theme, EmojiCode = customEmoji });
            }
            else
            {
                _themePool.Add(userID, new List<ArtTheme>() { new ArtTheme { Theme = theme, EmojiCode = customEmoji } });
            }
            
            Save();
            return true;
        }
                
        public bool RemoveThemeFromPool(string theme)
        {
            theme = theme.ToLower().Trim();
            
            var pair = FindArtThemeByTheme(theme);
            if (pair == default)
                return false;

            return RemoveThemeFromPool(pair.Item1, theme);
        }

        public bool RemoveThemeFromPool(ulong userID, string theme)
        {
            theme = theme.ToLower().Trim();

            List<ArtTheme> themes;
            if (_themePool.TryGetValue(userID, out themes))
            {
                var first = themes.FirstOrDefault(x => x.Theme == theme);
                if (first == default)
                    return false;

                if (!themes.Remove(first))
                    return false;

                if (themes.Count <= 0 && !_themePool.Remove(userID))
                    return false;
            }
            else
                return false;

            Save();
            return true;
        }

        public bool IsGDriveOn()
        {
            return _gDriveOn;
        }

        public void SetGDriveOn(bool bOn)
        {
            _gDriveOn = bOn;
        }

        public List<ulong> GetSubscribers()
        {
            return _subscribers;
        }

        public ulong GetThemePollMessageId()
        {
            return _msgIDs[(int)MsgIDType.ThemePoll];
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

        public TradeSegment GetActiveTradeSegment()
        {
            return _artTradeActive;
        }

        public bool ChangeSubscription(ulong userID, ref bool ? bOnOff)
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
            {
                bOnOff = false;
                _subscribers.Remove(userID);
            }
            else
            {
                bOnOff = true;
                _subscribers.Add(userID);
            }

            Save();
            return true;
        }

        public void SetThemePollID(ulong id)
        {
            _msgIDs[(int)MsgIDType.ThemePoll] = id;
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

        public void ActivateTrade(TradeSegment? seg, double? days2start, double? days2end, bool? bForce, bool? bGDriveOn, bool? bResetPoll)
        {
            if (seg.HasValue)
                _artTradeActive = seg.Value;

            _notified = NofifyFlags.None;

            switch (_artTradeActive)
            {
                case TradeSegment.TradeMonth:
                    _tradeStart = DateTime.Now;
                    break;
                case TradeSegment.EntryWeek:
                    break;
            }

            if (days2start.HasValue)
                _tradeStart = _tradeStart.AddDays(days2start.Value);

            if (days2end.HasValue)
                _tradeDays = days2end.Value;

            if (bForce.HasValue)
                _forceTradeEnd = bForce.Value;

            if (bGDriveOn.HasValue)
                _gDriveOn = bGDriveOn.Value;

            if (bResetPoll.HasValue && bResetPoll.Value == true)
                _msgIDs[(int)MsgIDType.ThemePoll] = 0;

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
        public override void Clear() 
        { 
            _artTradeActive = TradeSegment.EntryWeek;
            _notified = NofifyFlags.None;
            _msgIDs[(int)MsgIDType.Help] = 0;
            _msgIDs[(int)MsgIDType.NaughtyList] = 0;
            _msgIDs[(int)MsgIDType.ThemePoll] = 0;
            _subscribers.Clear();

        }
        public override void Load(string path = null)
        {
            string json = File.ReadAllText(path == null ? _path : path);
            var data = JsonConvert.DeserializeObject<ApplicationSettings>(json);
            if (data != null)
            {
                _artTradeActive = data.GetActiveTradeSegment();
                _workingChannel = data.GetWorkingChannel();
                _tradeStart = data.GetTradeStart();
                _tradeDays = data.GetTradeDays();
                _notified = data.GetNotifyFlags();
                _forceTradeEnd = data.IsForceTradeOn();
                _msgIDs = data.GetMsgIDs();
                _subscribers = data.GetSubscribers();
                _themePool = data.GetThemePool();
                _gDriveOn = data.IsGDriveOn();
            }
        }
        public override void Save(string path = null)
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path == null ? _path : path, json);
        }
    }
}
