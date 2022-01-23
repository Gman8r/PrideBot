using PrideBot.Plushies;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class Plushie
    {
        [PrimaryKey]
        [DontPushToDatabase]
        public string PlushieId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PlushieContext Context { get; set; }
        public decimal Weight { get; set; }
    }
}
