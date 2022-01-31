using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public enum PlushieTransaction
    {
        None = 0,
        Drawn = 1,
        Traded = 2,
        Using = 3,
        Done = 4,
        Plushie = 5, // removed by some plushie effect
        Void = 6 // removed through admin/debug methods
    }
}
