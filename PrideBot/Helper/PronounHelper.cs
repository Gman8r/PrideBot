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

namespace PrideBot
{
    public enum Pronoun
    {
        They = 0,
        Them = 1,
        Their = 2,
        Theirs = 3,
        Themself = 4
    }

    public static class PronounHelper
    {

        public static PronounSet[] AllPronouns = new PronounSet[]
        {
            new PronounSet("they/them", true,
                new string[] { "they", "them", "their", "theirs", "themself" }),

            new PronounSet("he/him", false,
                new string[] { "he", "him", "his", "his", "himself" }),

            new PronounSet("she/her", false,
                new string[] { "she", "her", "her", "hers", "herself" }),

            new PronounSet("it/its", false,
                new string[] { "it", "it", "its", "its", "itself" })
        };

        public class PronounSet
        {
            public string Name { get; }
            public bool IsPlural { get; }
            public string [] Prounouns { get; }

            public PronounSet(string name, bool isPlural, string[] prounouns)
            {
                Name = name;
                IsPlural = isPlural;
                Prounouns = prounouns;
            }
        }

        public static string Pronoun(this SocketUser user, DiscordSocketClient client, Pronoun usage, string pluralSuffix = "", string singularSuffix = "", bool capitalize = false)
            => Pronoun(client.Guilds
                .Select(a => a.GetUser(user.Id))
                .Where(a => a != null)
                .SelectMany(a => a.Roles), usage, pluralSuffix, singularSuffix, capitalize);

        public static string Pronoun(this SocketGuildUser user, Pronoun usage, string pluralSuffix = "", string singularSuffix = "", bool capitalize = false)
            => Pronoun(user.Roles, usage, pluralSuffix, singularSuffix, capitalize);

        public static string Pronoun(IEnumerable<SocketRole> roles, Pronoun usage, string pluralSuffix = "", string singularSuffix = "", bool capitalize = false)
        {
            // Get preferred pronoun roles
            var matches = AllPronouns
                .Where(a => roles
                    .Any(aa => aa.Name.Contains(a.Name, StringComparison.OrdinalIgnoreCase)
                    && aa.Name.Contains("preferred", StringComparison.OrdinalIgnoreCase)));

            // Get regular pronoun roles
            if (!matches.Any())
                matches = AllPronouns
                .Where(a => roles
                    .Any(aa => aa.Name.Contains(a.Name, StringComparison.OrdinalIgnoreCase)));

            var match = matches.FirstOrDefault() ?? AllPronouns.First();
            var usageStr = match.Prounouns[(int)usage];
            var suffixStr = match.IsPlural ? pluralSuffix : singularSuffix;
            if (capitalize)
                usageStr = ((char)(usageStr.First() + ('A' - 'a'))).ToString() + usageStr.Substring(1);
            return usageStr + suffixStr;
        }

        public static string Honorific(this SocketUser user, DiscordSocketClient client, string sheResponse, string heResponse, string theyResponse)
            => Honorific(client.Guilds
                .Select(a => a.GetUser(user.Id))
                .Where(a => a != null)
                .SelectMany(a => a.Roles), sheResponse, heResponse, theyResponse);

        public static string Honorific(this SocketGuildUser user, string sheResponse, string heResponse, string theyResponse)
            => Honorific(user.Roles, sheResponse, heResponse, theyResponse);

        public static string Honorific(IEnumerable<SocketRole> roles, string sheResponse, string heResponse, string theyResponse)
        {
            var pronoun = Pronoun(roles, PrideBot.Pronoun.They);
            if (pronoun.Equals("she"))
                return sheResponse;
            else if (pronoun.Equals("he"))
                return heResponse;
            return theyResponse;
        }
    }
}
