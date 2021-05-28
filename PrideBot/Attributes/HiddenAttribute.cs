using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    class HiddenAttribute : Attribute
    {
    }
}
