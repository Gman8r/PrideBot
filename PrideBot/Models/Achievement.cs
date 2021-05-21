using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class Achievement
    {
        [PrimaryKey]
        public string AchievementId { get; set; }
        public string Emoji { get; set; }
        public string Description { get; set; }
        public string Flavor { get; set; }
        public int DefaultScore { get; set; }
        public int CooldownHours { get; set; }
        public bool Log { get; set; }
        public bool Manual { get; set; }
        public bool Ping { get; set; }
    }
}
