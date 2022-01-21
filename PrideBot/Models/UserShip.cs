using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class UserShip : Ship
    {
        [PrimaryKey]
        public int Tier { get; set; }
        [PrimaryKey]
        public string UserId { get; set; }
        new public string ShipId { get; set; }
        public string Heart1 { get; set; }
        public string Heart1Right { get; set; }
        public string Heart2 { get; set; }
        public string Heart2Right { get; set; }
        [DontPushToDatabase]
        public decimal ScoreRatio { get; set; }
        [DontPushToDatabase]
        public decimal PointsEarnedByUser { get; set; }
    }
}
