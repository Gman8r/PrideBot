using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class RecentScore
    {
        public string AchievementId { get; set; }
        public int Count { get; set; }
        public string Description { get; set; }
        public DateTime LastTime { get; set; }
    }
}
