using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace teanicorns_art_trade_bot.Storage
{
    public class UserData : ICloneable
    {
        public ulong UserId = 0;
        public string UserName = "";
        public string ReferenceUrl = "";
        public string ReferenceDescription = "";
        public string NickName = "";
        public string ArtUrl = "";
        public ulong PreferenceId = 0;

        [JsonConstructor]
        public UserData(ulong id)
        {
            UserId = id;
        }

        public UserData(ulong id, string username)
        {
            UserId = id;
            UserName = username;
        }

        // ICloneable
        public object Clone()
        {
            UserData clone = (UserData)MemberwiseClone();
            //clone.ThemePool = new List<string>(ThemePool);
            return clone;
        }
    }
}
