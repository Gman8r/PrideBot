using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Plushie
{
    [Flags]
    public enum PlushieContext
    {
        None = 0,
        PlushieMenu = 1 << 0,
        QuizChoiceStart = 1 << 1,
        QuizBeforeStart = 1 << 2,
    }
}
