﻿using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    public static class GameHelper
    {
        //public static decimal GetPointFraction(UserShipTier tier) => tier == UserShipTier.Primary ? 1m
        //    : (tier == UserShipTier.Secondary ? .4m : .2m);
        //public static int GetPointPercent(UserShipTier tier) => (int)(GetPointFraction(tier) * 100m);
        public static int GetPointPercent(Decimal mult) => (int)(mult * 100m);

        public static bool EventStarted(IConfigurationRoot config) => DateTime.Now.Month == int.Parse(config["eventmonth"]);

        public static int GetQuizDay(DateTime atTime) => atTime.Day;

        public static int GetQuizDay() => GetQuizDay(DateTime.Now);

        public static SocketGuild GetGyn(this DiscordSocketClient client, IConfigurationRoot config)
            => client.GetGuild(ulong.Parse(config["ids:gyn"]));

        public static SocketRole GetRoleFromConfig(this SocketGuild guild, IConfigurationRoot config, string subKey)
            => guild.GetRole(ulong.Parse(config[$"ids:{subKey}"]));

        public static SocketChannel GetChannelfromConfig(this SocketGuild guild, IConfigurationRoot config, string subKey)
            => guild.GetChannel(ulong.Parse(config[$"ids:{subKey}"]));
    }
}
