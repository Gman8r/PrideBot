using PrideBot.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    public static class GameHelper
    {
        public static decimal GetPointFraction(UserShipTier tier) => tier == UserShipTier.Primary ? 1m
            : (tier == UserShipTier.Secondary ? .4m : .2m);
    }
}
