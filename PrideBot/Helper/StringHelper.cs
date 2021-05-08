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

        public static string CamelCaseSpaces(string name, bool uppercaseWords = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            name = (uppercaseWords ? name[0].ToString().ToUpper() : name[0].ToString().ToLower()) + name.Substring(1);
            var result = "";
            foreach (var chr in name)
            {
                if (chr >= 'A' && chr <= 'Z')
                    result += " " + (uppercaseWords ? chr.ToString() : chr.ToString().ToLower());
                else
                    result += chr.ToString();

            }
            return result.Trim();
        }
    }
}
