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
    public class ApplicationData : IStorage, ICloneable
    {
        public string Theme = "";
        public List<UserData> Storage = new List<UserData>();
        private Shuffle m_shuffle = new Shuffle();
        public UserData TryGetValue(ulong userId, out int index)
        {
            index = Storage.FindIndex(x => x.UserId == userId);
            if (index != -1)
            {
                return Storage[index];
            }

            return null;
        }

        public void AddOrSetValue(UserData data)
        {
            int index = Storage.FindIndex(x => x.UserId == data.UserId);
            if (index != -1)
            {
                if (!string.IsNullOrWhiteSpace(data.ReferenceDescription))
                    Storage[index].ReferenceDescription = data.ReferenceDescription;
                if (!string.IsNullOrWhiteSpace(data.ReferenceUrl))
                    Storage[index].ReferenceUrl = data.ReferenceUrl;
                if (!string.IsNullOrWhiteSpace(data.NickName))
                    Storage[index].NickName = data.NickName;
                if (!string.IsNullOrWhiteSpace(data.ArtUrl))
                    Storage[index].ArtUrl = data.ArtUrl;
            }
            else
                Storage.Add(data);
        }

        public bool TryRemoveValue(ulong userId)
        {
            int index = Storage.FindIndex(x => x.UserId == userId);
            if (index != -1)
            {
                Storage.RemoveAt(index);
                return true;
            }

            return false;
        }

        public UserData GetNextValue(ulong userId, out int index)
        {
            index = Storage.FindIndex(x => x.UserId == userId);
            if (index != -1)
            {
                ++index;
                index = (index == Storage.Count ? 0 : index);
                return Storage[index];
            }

            return null;
        }

        public UserData GetPreviousValue(ulong userId, out int index)
        {
            index = Storage.FindIndex(x => x.UserId == userId);
            if (index != -1)
            {
                --index;
                index = (index == -1 ? Storage.Count - 1 : index);
                return Storage[index];
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

            Storage[theirIndex] = ourUser;
            Storage[ourIndex] = theirUser;
            return true;
        }

        // public methods
        public bool SetTheme(string theme)
        {
            Theme = theme;
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
        public void Shuffle(ApplicationHistory history)
        {
            if (!m_shuffle.Compute(this, history))
                Storage = Storage.OrderBy(x => Guid.NewGuid()).ToList();
            Save();
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
                if (Storage.Count < 2)
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
                if (Storage.Count < 3)
                    return false;

                LinkedList<UserData> linkedList = new LinkedList<UserData>(Storage);

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

                Storage = linkedList.ToList();
            }

            Save();
            return true;
        }
        public List<UserData> GetStorage()
        {
            return Storage;
        }

        // IStorage
        public string FileName() { return Axx.AppDataFileName; }
        public int Count() { return Storage.Count; }
        public void Clear() { Storage.Clear(); }
        public void Load(string fileName)
        {
            string json = File.ReadAllText(fileName);
            var data = JsonConvert.DeserializeObject<ApplicationData>(json);
            if (data != null)
                Axx.AppData = data;
        }
        public void Save()
        {
            //if (this == Axx.AppData)
            //{
                string json = JsonConvert.SerializeObject(Axx.AppData, Formatting.Indented);
                File.WriteAllText(Axx.AppDataFileName, json);
            //}
            //else
            //{
                //Axx.AppHistory.Save();
            //}
        }

        // IClonable
        public object Clone()
        {
            ApplicationData clone = (ApplicationData)MemberwiseClone();
            clone.Storage = clone.Storage.Select(x => (UserData)x.Clone()).ToList();
            return clone;
        }
    }
}
