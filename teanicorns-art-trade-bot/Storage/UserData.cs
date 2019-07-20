using System;
using System.Collections.Generic;
using System.Text;

namespace teanicorns_art_trade_bot.Storage
{
    public class UserData : ICloneable
    {
        public ulong UserId;
        public string UserName;
        public string ReferenceUrl;
        public string ReferenceDescription;
        public string NickName;
        public string ArtUrl;

        // ICloneable
        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
