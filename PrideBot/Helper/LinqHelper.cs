using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    static class LinqHelper
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TResult>(this IEnumerable<TSource> ienumerable, Func<TSource, TResult> selector)
        {
            ienumerable.Select(a => a.ToString());
            return ienumerable
                .GroupBy(selector)
                .Select(a => a.First());
        }

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> dict)
            => dict.ToDictionary(t => t.Key, t => t.Value);
    }
}
