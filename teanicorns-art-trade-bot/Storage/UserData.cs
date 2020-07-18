using System;
using System.Collections.Generic;
using System.Text;

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
        public List<string> ThemePool = new List<string>();

        // ICloneable
        public object Clone()
        {
            UserData clone = (UserData)MemberwiseClone();
            clone.ThemePool = new List<string>(ThemePool);
            return clone;
        }
    }
}
