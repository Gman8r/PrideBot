using PrideBot.Plushie;
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
        public bool Name { get; set; }
        public PlushieContext Context { get; set; }
        public decimal Weight { get; set; }
    }
}
