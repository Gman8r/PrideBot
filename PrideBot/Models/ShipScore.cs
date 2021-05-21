using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class ShipScore : Score
    {
        [PrimaryKey]
        public string ShipId { get; set; }
        public int Tier { get; set; }
    }
}