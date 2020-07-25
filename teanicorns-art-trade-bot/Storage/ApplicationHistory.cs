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
    public class ApplicationHistory : IStorage
    {
        [JsonProperty] private List<ApplicationData> History = new List<ApplicationData>();

        public List<ApplicationData> GetHistory()
        {
            return History;
        }

        public ApplicationData GetTrade(int idx)
        {
            return (idx < Count() && idx >= 0 ? History[idx] : null);
        }

        public void RecordTrade(ApplicationData d)
        {
            var clone = (ApplicationData)d.Clone();
            History.Insert(0, clone);
            Save();
        }

        // IStorage
        public string FileName() { return xs.HISTORY_PATH; }
        public int Count() { return History.Count; }
        public void Clear() { History.Clear(); }
        public void Load(string fileName)
        {
            string json = File.ReadAllText(fileName);
            var data = JsonConvert.DeserializeObject<ApplicationHistory>(json);
            if (data != null)
                xs.History = data;
        }
        public void Save()
        {
            string json = JsonConvert.SerializeObject(xs.History, Formatting.Indented);
            File.WriteAllText(xs.HISTORY_PATH, json);
        }
    }
}
