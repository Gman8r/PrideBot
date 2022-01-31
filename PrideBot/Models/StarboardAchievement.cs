using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class StarboardAchievement
    {
        [PrimaryKey]
        public int StarCount { get; set; }
        public string AchievementId { get; set; }
    }
}
