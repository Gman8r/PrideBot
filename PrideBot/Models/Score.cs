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
        public string UserId { get; set; }
        public string AchievementId { get; set; }
        public int PointsEarned { get; set; }

        [DontPushToDatabase]
        public DateTime TimeStamp { get; set; }
        [DontPushToDatabase]
        public int ShipCount { get; set; }
        public string Approver { get; set; }
    }
}
