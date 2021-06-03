using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    static class MathHelper
    {
        public static double Rad2Deg = 180d / Math.PI;

        public static double Deg2Rad = Math.PI / 180d;

        /// <summary>
        /// Returns modulus that works with negative numbers (always between 0 and m)
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public static int TrueMod(int x, int m)
        {
            int r = x % m;
            while (r < 0f)
            {
                r += m;
            }
            return r;
        }

        /// <summary>
        /// Returns modulus that works with negative numbers (always between 0 and m)
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public static float TrueMod(float x, float m)
        {
            float r = x % m;
            while (r < 0f)
            {
                r += m;
            }
            return r;
        }

        /// <summary>
        /// Returns modulus that works with negative numbers (always between 0 and m)
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public static double TrueMod(double x, double m)
        {
            double r = x % m;
            while (r < 0f)
            {
                r += m;
            }
            return r;
        }

        public static string ToPercent(decimal d) => (d * 100).ToString();

        public static string GetPlacePrefix(int num)
        {
            num %= 100;
            switch (num)
            {
                case (0):
                    return "th";
                case (1):
                    return "st";
                case (2):
                    return "nd";
                case (3):
                    return "rd";
            }
            if (num <= 20)
                return "th";
            return GetPlacePrefix(num % 10);

        }
    }
}
