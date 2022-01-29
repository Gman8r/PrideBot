using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PrideBot
{
    static class StringHelper
    {
        public static string ToAlphaNumeric(this string name)
        {
            var result = "";
            foreach (var chr in name)
            {
                if ((chr >= 'a' && chr <= 'z') || (chr >= 'A' && chr <= 'Z') || (chr >= '0' && chr <= '9'))
                    result += chr;
            }
            return result;
        }

        public static string CapitalizeFirst(this string str)
            => str.Substring(0, 1).ToUpper() + str.Substring(1);

        public static string CamelCaseSpaces(string name, bool uppercaseWords = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            name = (uppercaseWords ? name[0].ToString().ToUpper() : name[0].ToString().ToLower()) + name.Substring(1);
            var result = "";
            for (int i = 0; i < name.Length; i++)
            {
                var chr = name[i];

                if (i > 0 && char.IsDigit(chr) && char.IsLetter(name[i - 1]))
                    result += " " + chr;
                else if (chr >= 'A' && chr <= 'Z')
                    result += " " + (uppercaseWords ? chr.ToString() : chr.ToString().ToLower());
                else
                    result += chr.ToString();

            }
            return result.Trim();
        }

        public static string EmptyCoalesce(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            return null;
        }

        public static string WhitespaceCoalesce(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }
    }
}
