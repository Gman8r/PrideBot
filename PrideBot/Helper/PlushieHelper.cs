using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using PrideBot.Plushies;
using PrideBot.Models;

namespace PrideBot.Helper
{
    public static class PlushieHelper
    {
        //public static ComponentBuilder GetUserPlushiesDropdown(IEnumerable<UserPlushie> plushies)
        //{
        //    //return plushies
        //    //    .Select(a => new SelectMenuOptionBuilder(a.CharacterName, a.UserPlushieChoiceId.ToString(), a.Name))
        //    //    .ToList();
        //}
    }
}
