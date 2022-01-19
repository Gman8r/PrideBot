using PrideBot.Plushie;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class UserPlushie : Plushie
    {
        [PrimaryKey]
        [DontPushToDatabase]
        public int UserPlushieId { get; set; }
        public string CharacterId { get; set; }
        public string UserId { get; set; }
        public string OriginalUserId{ get; set; }
    }
}
