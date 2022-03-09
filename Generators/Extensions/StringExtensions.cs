using System;
using System.Collections.Generic;
using System.Text;

namespace Generators.Extensions
{
    internal static class StringExtensions
    {
        internal static string StringJoin<T>(this IEnumerable<T> source, string separator = "")
        { 
            return string.Join(separator, source);
        }

        internal static int Multiply(this IEnumerable<int> source)
        {
            var sum = 1;
            foreach (var item in source) 
            {
                sum *= item;
            }

            return sum;
        }

        internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }
    }
}
