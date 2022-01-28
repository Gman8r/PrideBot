using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public enum PlushieEffectContext
    {
        None = 0,
        Score = 1,  // when a plushie is used on a score's base value
        ShipScore = 2, // when a plushie is used on one of a score's ship values
        Quiz = 3,   // when a plushie is used on a quiz
        Timeout = 4,    // when a plushie is used on a timed card
        Message = 5,    // when a plushie use is tied to a message (for bonuses)
        Interaction  = 6,   // when a plushie use is tied to an interaction (unused?)
        Decimal = 7 // when a plushie's use  has an associated random number (often for stored bonuses e.g. LUCK_OF_DRAW)
    }
}