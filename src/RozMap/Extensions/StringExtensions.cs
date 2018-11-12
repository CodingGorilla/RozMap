using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace RozMap.Extensions
{
    internal static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string value)
        {
            return String.IsNullOrEmpty(value);
        }
        public static bool IsNotNullOrEmpty(this string value)
        {
            return !String.IsNullOrEmpty(value);
        }

        public static IEnumerable<string> ReadLines(this string text)
        {
            var reader = new StringReader(text);
            string line;
            while((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }

        public static string SanitizedName(this Type type)
        {
            var result = Regex.Replace(type.Name, @"[^\w\d_]", "_");
            return result;
        }

        /// <summary>
        /// Concatenates a string between each item in a list of strings
        /// </summary>
        /// <param name="values">The array of strings to join</param>
        /// <param name="separator">The value to concatenate between items</param>
        /// <returns></returns>
        public static string Join(this IEnumerable<string> values, string separator)
        {
            return string.Join(separator, values);
        }
    }
}