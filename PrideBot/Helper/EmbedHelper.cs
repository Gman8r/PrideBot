using Discord;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    public static class EmbedHelper
    {
        public static Color GetEventColor(IConfigurationRoot config) => new Color(uint.Parse(config["eventcolor"]));
    }
}