using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    public static class ServicesHelper
    {
        public static T GetService<T>(this IServiceProvider provider)
            => (T)provider.GetService(typeof(T));
    }
}
