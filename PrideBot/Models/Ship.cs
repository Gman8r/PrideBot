using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    class Ship
    {
        [PrimaryKey]
        public string ShipId { get; set; }
        public string CharacterId1 { get; set; }
        public string CharacterId2 { get; set; }
    }
}
