using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Game
{
    [Flags]
    public enum EventPeriod
    {
        None = 0,
        BeforeEvent = 1,
        DuringEvent = 2,
        AfterEvent = 4
    }
}
