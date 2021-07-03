using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class User
    {
        [PrimaryKey]
        public string UserId { get; set; }
        public bool ShipsSelected { get; set; }
        public int CardBackground { get; set; }
        public bool PingForAchievements { get; set; }

        [DontPushToDatabase]
        public DateTime RegisteredAt { get; set; }
        [DontPushToDatabase]
        public int PointsEarned { get; set; }
        [DontPushToDatabase]
        public int PointsGivenToShips { get; set; }
    }
}
