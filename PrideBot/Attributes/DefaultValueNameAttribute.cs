using System;

namespace PrideBot
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class DefaultValueNameAttribute : Attribute
    {
        public string Text { get; }

        public DefaultValueNameAttribute(string text)
        {
            Text = text;
        }
    }
}
