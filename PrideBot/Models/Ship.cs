using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class Ship
    {
        [PrimaryKey]
        public string CharacterId1 { get; set; }
        [PrimaryKey]
        public string CharacterId2 { get; set; }
        public string AvatarUrl { get; set; }
        public string Nickname { get; set; }

        public string GetDisplayName() => Nickname ?? $"{CharacterId1.ToLower().CapitalizeFirst()} X {CharacterId2.ToLower().CapitalizeFirst()}";
    }
}
