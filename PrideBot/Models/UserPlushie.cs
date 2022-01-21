using PrideBot.Plushies;
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
        public decimal Rotation { get; set; }
        public string UserId { get; set; }
        public string OriginalUserId{ get; set; }
    }
}
