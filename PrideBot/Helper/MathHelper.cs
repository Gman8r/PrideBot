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
        public static float TrueMod(float x, float m)
        {
            float r = x % m;
            while (r < 0f)
            {
                r += m;
            }
            return r;
        }
    }
}
