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
    class Shuffle
    {
        private List<List<ulong>> m_computedChain = new List<List<ulong>>();

        private Dictionary<ulong, List<ulong>> m_data = new Dictionary<ulong, List<ulong>>();
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
            for (int i = m_computedChain.Count - 1; i >= 0; i--)
            {
                idx = m_computedChain[i].FindIndex(x => x == val);
                if (idx != -1)
                {
                    valSubChain = m_computedChain[i];
                    m_computedChain.RemoveAt(i);
                    break;
                }
            }

            idx = -1;
            for (int i = 0; i <  m_computedChain.Count; i++)
            {
                idx = m_computedChain[i].FindIndex(x => x == key);
                if (idx != -1)
                {
                    if (valSubChain == null)
                        m_computedChain[i].Insert(idx + 1, val);
                    else
                        m_computedChain[i] = m_computedChain[i].Concat(valSubChain).ToList();
                    break;
                }
            }

            if (idx == -1)
            {
                if (valSubChain == null)
                    m_computedChain.Add(new List<ulong>() { key, val });
                else
                {
                    if (valSubChain.Contains(key))
                    {
                        m_computedChain.Add(valSubChain);
                        return false; // cyclic dependency
                    }

                    m_computedChain.Add(valSubChain.Prepend(key).ToList());
                }
            }

            return true;
        }

        private bool SwitchChainLink(ulong key, ulong val)
        {
            int idx;
            for (int i = 0; i < m_computedChain.Count; i++)
            {
                idx = m_computedChain[i].FindIndex(x => x == key);
                if (idx != -1 && idx + 1 < m_computedChain[i].Count)
                {
                    m_computedChain[i][idx] = val;
                    m_computedChain[i][idx + 1] = key;
                    return true;
                }
            }

            return false;
        }
        private bool Initialize(ApplicationData currentTrade, ApplicationHistory tradeHistory)
        {
            m_data.Clear();
            m_computedChain.Clear();

            Dictionary<ulong, ulong> prevMonthList = null;
            if (tradeHistory.History.Count > 0)
                prevMonthList = FillPreviousTradesPairs(currentTrade, tradeHistory.History[0]);

            Dictionary<ulong, ulong> prevPrevMonthList = null;
            if (tradeHistory.History.Count > 1)
                prevPrevMonthList = FillPreviousTradesPairs(currentTrade, tradeHistory.History[1]);

            foreach (UserData user1 in currentTrade.Storage)
            {
                List<ulong> candidate = new List<ulong>();

                ulong prevPartner = 0;
                if (prevMonthList != null)
                    prevMonthList.TryGetValue(user1.UserId, out prevPartner);

                ulong prevPrevPartner = 0;
                if (prevPrevMonthList != null)
                    prevPrevMonthList.TryGetValue(user1.UserId, out prevPrevPartner);

                bool bPushBack = false;
                foreach (UserData user2 in currentTrade.Storage)
                {
                    if (user2.UserId == user1.UserId || user2.UserId == prevPartner)
                        continue;

                    if (user2.UserId == prevPrevPartner)
                        bPushBack = true;
                    else
                        candidate.Add(user2.UserId);
                }

                candidate = candidate.OrderBy(x => Guid.NewGuid()).ToList();
                if (bPushBack)
                    candidate.Add(prevPrevPartner);

                if (candidate.Count == 0)
                    return false;
                m_data.Add(user1.UserId, candidate);
            }

            if (m_data.Count == 0)
                return false;
            m_data = m_data.OrderBy(x => x.Value.Count).ToDictionary(x => x.Key, x => x.Value);
            return true;
        }
        
        public bool Compute(ApplicationData currentTrade, ApplicationHistory tradeHistory)
        {
            if (!Initialize(currentTrade, tradeHistory))
                return false;

            Dictionary<ulong, ulong> computedPairs = new Dictionary<ulong, ulong>();
            var dataKeys = m_data.Keys.ToList();
            while (dataKeys.Count > 0)
            {
                var key = dataKeys.First();
                dataKeys.RemoveAt(0);
                if (computedPairs.Keys.Contains(key))
                    continue;

                ulong overrideBackedge = 0;
                foreach (ulong value in m_data[key])
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

            if (m_computedChain.Count != 1)
                return false;

            Console.WriteLine("Shuffle:\n");
            List<UserData> finalOrder = new List<UserData>();
            foreach (ulong id in m_computedChain.First())
            {
                int idx;
                UserData dat = currentTrade.TryGetValue(id, out idx);
                if (dat == null)
                    return false;
                finalOrder.Add(dat);
                Console.WriteLine($"{dat.UserName} {dat.NickName}");
            }

            if (finalOrder.Count != currentTrade.Storage.Count)
                return false;

            currentTrade.Storage = finalOrder;
            return true;
        }
    }
}
