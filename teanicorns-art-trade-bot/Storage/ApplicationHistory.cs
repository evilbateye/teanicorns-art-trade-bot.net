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
    public class ApplicationHistory : StorageBase
    {
        [JsonProperty("History")] private List<ApplicationData> _history = new List<ApplicationData>();

        public List<ApplicationData> GetHistory()
        {
            return _history;
        }

        public ApplicationData GetTrade(int idx)
        {
            return (idx < Count() && idx >= 0 ? _history[idx] : null);
        }

        public void RecordTrade(ApplicationData d)
        {
            var clone = (ApplicationData)d.Clone();
            clone.SetParent(this);
            _history.Insert(0, clone);
            Save();
        }

        // StorageBase
        public override int Count() { return _history.Count; }
        public override void Clear() { _history.Clear(); }
        public override StorageBase Load(string path = null)
        {
            string json = File.ReadAllText(path == null ? _path : path);
            var data = JsonConvert.DeserializeObject<ApplicationHistory>(json);
            if (data != null)
            {
                data.GetHistory().ForEach(x => x.SetParent(data));
                data.SetPath(_path);
            }
            return data;
        }
        public override void Save(string path = null)
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path == null ? _path : path, json);
        }
    }
}
