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
    public class ApplicationData : StorageBase, ICloneable
    {
        [JsonProperty("Theme")] private string _theme = "";
        [JsonProperty("Storage")] private List<UserData> _storage = new List<UserData>();
        private AdvancedShuffle _shuffle = new AdvancedShuffle();
        
        public string GetTheme()
        {
            return _theme;
        }

        public List<UserData> GetStorage()
        {
            return _storage;
        }

        public void SetStorage(List<UserData> storage)
        {
            _storage = storage;
            Save();
        }

        public UserData TryGetValue(ulong userId, out int index)
        {
            index = _storage.FindIndex(x => x.UserId == userId);
            if (index != -1)
            {
                return _storage[index];
            }

            return null;
        }

        public void AddOrSetValue(UserData data)
        {
            int index = _storage.FindIndex(x => x.UserId == data.UserId);
            if (index != -1)
            {
                if (!string.IsNullOrWhiteSpace(data.ReferenceDescription))
                    _storage[index].ReferenceDescription = data.ReferenceDescription;
                if (!string.IsNullOrWhiteSpace(data.ReferenceUrl))
                    _storage[index].ReferenceUrl = data.ReferenceUrl;
                if (!string.IsNullOrWhiteSpace(data.NickName))
                    _storage[index].NickName = data.NickName;
                if (!string.IsNullOrWhiteSpace(data.ArtUrl))
                    _storage[index].ArtUrl = data.ArtUrl;
            }
            else
                _storage.Add(data);
        }

        public bool TryRemoveValue(ulong userId)
        {
            int index = _storage.FindIndex(x => x.UserId == userId);
            if (index != -1)
            {
                _storage.RemoveAt(index);
                return true;
            }

            return false;
        }

        public UserData GetNextValue(ulong userId, out int index)
        {
            index = _storage.FindIndex(x => x.UserId == userId);
            if (index != -1)
            {
                ++index;
                index = (index == _storage.Count ? 0 : index);
                return _storage[index];
            }

            return null;
        }

        public UserData GetPreviousValue(ulong userId, out int index)
        {
            index = _storage.FindIndex(x => x.UserId == userId);
            if (index != -1)
            {
                --index;
                index = (index == -1 ? _storage.Count - 1 : index);
                return _storage[index];
            }

            return null;
        }

        public bool SetNextValue(ulong ourId, ulong theirId)
        {
            int theirIndex;
            UserData theirUser = TryGetValue(theirId, out theirIndex);
            if (theirUser == null)
                return false;

            int ourIndex;
            UserData ourUser = TryGetValue(ourId, out ourIndex);
            if (ourUser == null)
                return false;

            _storage[theirIndex] = ourUser;
            _storage[ourIndex] = theirUser;
            return true;
        }

        // public methods
        public bool SetTheme(string theme)
        {
            _theme = theme;
            Save();
            return true;
        }
        public void Set(UserData data)
        {
            AddOrSetValue(data);
            Save();
        }
        public UserData Get(ulong userId)
        {
            int index;
            return TryGetValue(userId, out index);
        }
        public bool Remove(ulong userId)
        {
            if (TryRemoveValue(userId))
            {
                Save();
                return true;
            }

            return false;
        }
        public void DoShuffle(ApplicationHistory history)
        {
            if (!_shuffle.Compute(this, history))
            {
                _storage = _storage.OrderBy(x => Guid.NewGuid()).ToList();
                Save();
            }
        }
        public bool Next(ulong userId, out UserData nextUser)
        {
            int index;
            nextUser = GetNextValue(userId, out index);
            return nextUser != null;
        }
        public bool Previous(ulong userId, out UserData previousUser)
        {
            int index;
            previousUser = GetPreviousValue(userId, out index);
            return previousUser != null;
        }
        public bool ResetNext(ulong ourId, ulong theirId, ulong? thirdId, out List<UserData> needNotify)
        {
            needNotify = new List<UserData>();

            if (!thirdId.HasValue)
            {
                if (_storage.Count < 2)
                    return false;

                HashSet<UserData> needNotifySet = new HashSet<UserData>();

                UserData user = null;
                if (!Next(ourId, out user))
                    return false;
                needNotifySet.Add(user);

                if (!Next(theirId, out user))
                    return false;
                needNotifySet.Add(user);

                user = Get(ourId);
                if (user == null)
                    return false;
                needNotifySet.Add(user);

                user = Get(theirId);
                if (user == null)
                    return false;
                needNotifySet.Add(user);

                needNotify = needNotifySet.ToList();

                SetNextValue(ourId, theirId);
            }
            else
            {
                if (_storage.Count < 3)
                    return false;

                LinkedList<UserData> linkedList = new LinkedList<UserData>(_storage);

                Func<LinkedListNode<UserData>, ulong, (LinkedListNode<UserData>, ulong)> FindNode = (start, id) =>
                {
                    ulong distance = 0;
                    var node = start;
                    while (node != null)
                    {
                        if (node.Value.UserId == id)
                            break;
                        node = node.Next;
                        distance += 1;
                    }
                    return (node, distance);
                };

                List<(LinkedListNode<UserData>, ulong)> nodeDistPairs = new List<(LinkedListNode<UserData>, ulong)>();
                var foundNode = FindNode(linkedList.First, ourId);
                if (foundNode.Item1 == null)
                    return false;
                nodeDistPairs.Add(foundNode);
                needNotify.Add(foundNode.Item1.Value);

                foundNode = FindNode(linkedList.First, theirId);
                if (foundNode.Item1 == null)
                    return false;
                nodeDistPairs.Add(foundNode);
                needNotify.Add(foundNode.Item1.Value);

                foundNode = FindNode(linkedList.First, thirdId.Value);
                if (foundNode.Item1 == null)
                    return false;
                nodeDistPairs.Add(foundNode);
                needNotify.Add(foundNode.Item1.Value);

                nodeDistPairs = nodeDistPairs.OrderBy(x => x.Item2).ToList();

                var iNode = nodeDistPairs[1].Item1;
                while (iNode.Value.UserId != nodeDistPairs[0].Item1.Value.UserId)
                {
                    var prevNode = iNode.Previous;
                    linkedList.Remove(iNode);
                    linkedList.AddAfter(nodeDistPairs[2].Item1, iNode);
                    iNode = prevNode;
                }

                _storage = linkedList.ToList();
            }

            Save();
            return true;
        }

        // StorageBase
        public override int Count() { return _storage.Count; }
        public override void Clear() { _storage.Clear(); }
        public override void Load(string path = null)
        {
            string json = File.ReadAllText(path == null ? _path : path);
            var data = JsonConvert.DeserializeObject<ApplicationData>(json);
            if (data != null)
            {
                _theme = data.GetTheme();
                _storage = data.GetStorage();
            }
        }
        public override void Save(string path = null)
        {
            if (_parent != null)
            {
                _parent.Save(path); // RevealArt with theme set case
                return;
            }

            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path == null ? _path : path, json);
        }

        // IClonable
        public object Clone()
        {
            ApplicationData clone = (ApplicationData)MemberwiseClone();
            clone._storage = clone._storage.Select(x => (UserData)x.Clone()).ToList();
            return clone;
        }
    }
}
