using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    public static class SearchHelper
    {
        public static List<T> FindMatches<T>(List<T> searchList, string searchString, char[] separators)
        {
            searchString = searchString.ToLower();

            List<T> results = new List<T>();
            //separators == null ? searchString.Split() :
            string[] searchTerms = searchString.Split(separators);

            foreach (T searchable in searchList)
            {
                string searchableString = searchable.ToString();
                if (IsMatch(searchable.ToString(), searchTerms))
                {
                    results.Add(searchable);
                }
            }

            return results;
        }

        public static List<T> FindMatches<T>(List<T> searchList, string searchString, char separator = ' ')
        {
            return FindMatches<T>(searchList, searchString, new char[] { separator });
        }

        public static bool IsMatch(string searchableString, string[] searchTerms)
        {
            bool foundMatch = true;
            for (int i = 0; i < searchTerms.Length; i++)
            {
                //Console.WriteLine(searchTerms[i]);

                string[] words = searchableString.Split(' ');

                foundMatch = false;
                for (int j = 0; j < words.Length; j++)
                {
                    if (words[j].StartsWith(searchTerms[i], StringComparison.OrdinalIgnoreCase))
                    {
                        foundMatch = true;
                        //Console.WriteLine(searchTerms[i] + " works for " + words[j]);
                        j = words.Length;
                    }
                }

                if (!foundMatch)
                {
                    return false;
                }

            }
            return true;

        }
    }

}