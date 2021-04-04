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
    class AdvancedShuffle
    {
        private List<List<ulong>> _computedChain = new List<List<ulong>>();
        private Dictionary<ulong, List<ulong>> _data = new Dictionary<ulong, List<ulong>>();
        private static Random _rng = new Random();

        private Dictionary<ulong, ulong> FillPreviousTradesPairs(ApplicationData thisMonth, ApplicationData prevMonth)
        {
            Dictionary<ulong, ulong> prevMonthList = new Dictionary<ulong, ulong>();
            foreach (UserData user in prevMonth.GetStorage())
            {
                UserData nextUser;
                if (thisMonth.Get(user.UserId) != null && prevMonth.Next(user.UserId, out nextUser))
                    prevMonthList.Add(user.UserId, nextUser.UserId);
            }
            return prevMonthList;
        }

        private bool AddToChain(ulong key, ulong val)
        {
            int idx = -1;
            List<ulong> valSubChain = null;
            for (int i = _computedChain.Count - 1; i >= 0; i--)
            {
                idx = _computedChain[i].FindIndex(x => x == val);
                if (idx != -1)
                {
                    valSubChain = _computedChain[i];
                    _computedChain.RemoveAt(i);
                    break;
                }
            }

            idx = -1;
            for (int i = 0; i <  _computedChain.Count; i++)
            {
                idx = _computedChain[i].FindIndex(x => x == key);
                if (idx != -1)
                {
                    if (valSubChain == null)
                        _computedChain[i].Insert(idx + 1, val);
                    else
                        _computedChain[i] = _computedChain[i].Concat(valSubChain).ToList();
                    break;
                }
            }

            if (idx == -1)
            {
                if (valSubChain == null)
                    _computedChain.Add(new List<ulong>() { key, val });
                else
                {
                    if (valSubChain.Contains(key))
                    {
                        _computedChain.Add(valSubChain);
                        return false; // cyclic dependency
                    }

                    _computedChain.Add(valSubChain.Prepend(key).ToList());
                }
            }

            return true;
        }

        private bool SwitchChainLink(ulong key, ulong val)
        {
            int idx;
            for (int i = 0; i < _computedChain.Count; i++)
            {
                idx = _computedChain[i].FindIndex(x => x == key);
                if (idx != -1 && idx + 1 < _computedChain[i].Count)
                {
                    _computedChain[i][idx] = val;
                    _computedChain[i][idx + 1] = key;
                    return true;
                }
            }

            return false;
        }
        private bool Initialize(ApplicationData currentTrade, ApplicationHistory tradeHistory)
        {
            _data.Clear();
            _computedChain.Clear();

            Dictionary<ulong, ulong> prevMonthList = null;
            if (tradeHistory.Count() > 0)
                prevMonthList = FillPreviousTradesPairs(currentTrade, tradeHistory.GetTrade(0));

            Dictionary<ulong, ulong> prevPrevMonthList = null;
            if (tradeHistory.Count() > 1)
                prevPrevMonthList = FillPreviousTradesPairs(currentTrade, tradeHistory.GetTrade(1));

            Dictionary<UserData, List<ulong>> tmpData = new Dictionary<UserData, List<ulong>>();

            foreach (UserData user1 in currentTrade.GetStorage())
            {
                List<ulong> candidate = new List<ulong>();

                ulong prevPartner = 0;
                if (prevMonthList != null)
                    prevMonthList.TryGetValue(user1.UserId, out prevPartner);

                ulong prevPrevPartner = 0;
                if (prevPrevMonthList != null)
                    prevPrevMonthList.TryGetValue(user1.UserId, out prevPrevPartner);

                bool bPushBack = false;
                foreach (UserData user2 in currentTrade.GetStorage())
                {
                    if (user2.UserId == user1.UserId || user2.UserId == prevPartner || user2.UserId == user1.PreferenceId)
                        continue;

                    if (user2.UserId == prevPrevPartner)
                        bPushBack = true;
                    else
                        candidate.Add(user2.UserId);
                }

                candidate = candidate.OrderBy(x => Guid.NewGuid()).ToList();
                
                if (bPushBack)
                    candidate.Add(prevPrevPartner);

                if (user1.PreferenceId != 0)
                    candidate = candidate.Prepend(user1.PreferenceId).ToList();

                if (candidate.Count == 0)
                    return false;

                tmpData.Add(user1, candidate);
            }

            if (tmpData.Count == 0)
                return false;

            // order by number of candidates first, if someone had someone else as a partner las trade, it is removed from candidates this trade
            foreach (var byCount in tmpData.GroupBy(x => x.Value.Count))
            {
                foreach (var byPreference in byCount.GroupBy(x => x.Key.PreferenceId != 0))
                {
                    foreach (var dat in byPreference.OrderBy(x => Guid.NewGuid()))
                        _data.Add(dat.Key.UserId, dat.Value);
                }
            }

            //_data = _data.OrderBy(x => x.Value.Count).ToDictionary(x => x.Key, x => x.Value);
            return true;
        }
        
        public bool Compute(ApplicationData currentTrade, ApplicationHistory tradeHistory)
        {
            if (!Initialize(currentTrade, tradeHistory))
                return false;

            Dictionary<ulong, ulong> computedPairs = new Dictionary<ulong, ulong>();
            var dataKeys = _data.Keys.ToList();
            while (dataKeys.Count > 0)
            {
                var key = dataKeys.First();
                dataKeys.RemoveAt(0);
                if (computedPairs.Keys.Contains(key))
                    continue;

                ulong overrideBackedge = 0;
                foreach (ulong value in _data[key])
                {
                    if (key == value)
                        continue;
                    if (computedPairs.Values.Contains(value))
                        continue;

                    ulong backEdge = 0;
                    if (computedPairs.TryGetValue(value, out backEdge))
                    {
                        if (key == backEdge)
                        {
                            overrideBackedge = value;
                            continue;
                        }
                    }

                    if (AddToChain(key, value))
                    {
                        overrideBackedge = 0;
                        computedPairs.Add(key, value);
                        break;
                    }
                }

                if (overrideBackedge != 0)
                {
                    if (!SwitchChainLink(overrideBackedge, key))
                        return false;
                    computedPairs.Remove(overrideBackedge);
                    computedPairs.Add(key, overrideBackedge);
                    dataKeys.Add(overrideBackedge);
                }
            }

            if (_computedChain.Count != 1)
                return false;

            Console.WriteLine("AdvancedShuffle:\n");
            List<UserData> finalOrder = new List<UserData>();
            foreach (ulong id in _computedChain.First())
            {
                int idx;
                UserData dat = currentTrade.TryGetValue(id, out idx);
                if (dat == null)
                    return false;
                finalOrder.Add(dat);
                Console.WriteLine($"{dat.UserName} {dat.NickName}");
            }

            if (finalOrder.Count != currentTrade.Count())
                return false;

            currentTrade.SetStorage(finalOrder);
            return true;
        }
    }
}
