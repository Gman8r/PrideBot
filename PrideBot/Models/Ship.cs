using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class Ship
    {
        [PrimaryKey]
        public string ShipId { get; set; }
        [DontPushToDatabase]
        public string CharacterId1 { get; set; }
        [DontPushToDatabase]
        public string CharacterId2 { get; set; }
        [DontPushToDatabase]
        public string Nickname { get; set; }
        public int TestPlace { get; set; }
        public double TestScore { get; set; }

        [DontPushToDatabase]
        public bool IsBlacklisted { get; set; }
        [DontPushToDatabase]
        public string Character1Name { get; set; }
        [DontPushToDatabase]
        public string Character2Name { get; set; }
        [DontPushToDatabase]
        public string Character1First { get; set; }
        [DontPushToDatabase]
        public string Character2First { get; set; }
        [DontPushToDatabase]
        public int PointsEarned { get; set; }
        [DontPushToDatabase]
        public int Supporters { get; set; }
        [DontPushToDatabase]
        public double RandomSupporters { get; set; }
        [DontPushToDatabase]
        public long Place { get; set; }
        [DontPushToDatabase]
        public long UnderdogPlace { get; set; }
        [DontPushToDatabase]
        public long SoloPlace { get; set; }
        [DontPushToDatabase]
        public string TopSupporter { get; set; }

        public bool IsEmpty() => string.IsNullOrWhiteSpace(CharacterId1) || string.IsNullOrWhiteSpace(CharacterId2);

        public string GetDisplayName() => /*Nickname ??*/ $"{Character1First}X{Character2First}";
    }
}
