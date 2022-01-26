using PrideBot.Plushies;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class PlushieEffectLog
    {
        [PrimaryKey]
        public int UserPlushieId { get; set; }
        public PlushieEffectContext ContetType { get; set; }
        public string Context { get; set; }
        public DateTime Timestamp { get; set; }
        [DontPushToDatabase]
        public string PlushieId { get; set; }
        [DontPushToDatabase]
        public string PlushieName { get; set; }
    }
}
