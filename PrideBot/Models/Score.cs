using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class Score
    {
        [PrimaryKey]
        [DontPushToDatabase]
        public int ScoreId { get; set; }
        public int ScoreGroupId { get; set; }
        public string UserId { get; set; }
        public string ShipId { get; set; }
        public int Tier { get; set; }
        public string AchievementId { get; set; }
        public int PointsEarned { get; set; }

        [DontPushToDatabase]
        public DateTime TimeStamp { get; set; }
    }
}
