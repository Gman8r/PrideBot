using PrideBot.Models;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    public class DialogueDict
    {

        Dictionary<string, string> dict;
        static DialogueDict instance;

        private readonly ModelRepository repo;

        public DialogueDict(ModelRepository repo)
        {
            this.repo = repo;

            instance = this;
            dict = new Dictionary<string, string>();
            PullDialogueAsync().GetAwaiter();
        }

        public async Task PullDialogueAsync()
        {
            var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var results = await repo.GetAllDialogueAsync(connection);
            dict = results.ToDictionary(t => t.DialogueId, t => t.Content);
        }

        public static Dictionary<string, string> GetDict() => instance.dict;


        public static string GetNoBullshit(string key, params object[] args)
        {

            var variants = instance.dict.Keys
                .Where(a => a.StartsWith(key + "-"))
                .ToList();
            string chosenKey;
            if (variants.Any())
                chosenKey = variants[new Random().Next() % variants.Count];
            else if (instance.dict.ContainsKey(key))
                chosenKey = key;
            else
                return "(MISSING DIALOGUE OH NO MY BRAIN)";

            var str = string.Format(instance.dict[chosenKey]
                .Replace("{SP}", EmoteHelper.SPEmote.ToString())
                , args);
            return str;
        }

        public static string Get(string key, params object[] args) => RollBullshit(GetNoBullshit(key, args));

        public static string RollBullshit(string str, double chance = .1)
        {
            var rand = new Random();

            var bullshitPhrases = new List<string>() {
                    "Wowwwwwww.",
                    "SJFKJFsg.",
                    "Fortnite.",
                    "Minecraft.",
                    "Gay.",
                    "Gayyy.",
                    "I'm gay.",
                    "I'm trans.",
                    "God I'm cute.",
                    "Gamer.",
                    "ASFJHAJSH!!",
                    "KSHGSJVVH.",
                    "PFPShfhfh.",
                    "AAAkfsjkfsfdjsjs.",
                    "Gender??",
                    "Gender.",
                    "Gay rights!",
                    "Trans rights!",
                    "ACAB!",
                    "Worm.",
                    "Like comment and subscribe.",
                    "Brainrot got me like.",
                    "On God?",
                    "Yooooo.",
                    "AAAAAAA!",
                    "pspspsp here kitty.",
                    "Where the hell am I?",
                    "Tsuchinoko real.",
                    "I love women.",
                    "I want money.",
                    "What's your sign?",
                    "Gemini.",
                    "Women.",
                    "Toontown.",
                    "Miku Hatsune.",
                    "Daaaaamn.",
                    "Fuck.",
                    "Lasers!!",
                    "sus.",
                    "amogus.",
                    "01100111 01100001 01111001.",
                    "eat pant.",
                    "Skadoosh!",
                    "",
                    "Nice hair wow!!",
                    "Home of sexual.",
                    "Gay Gay Homo Sexual Gay."};

            var roll = rand.NextDouble() < chance;
            if (str.Length > 0 && char.IsPunctuation(str.Last()) && roll)
            {
                var phrase = bullshitPhrases[rand.Next() % bullshitPhrases.Count];
                if (str.Last() == '!' && phrase.Last() == '.')
                    phrase = phrase.Substring(0, phrase.Length - 1) + "!";
                str += " " + phrase;
                str = RollBullshit(str, .25);    // Double up baby
            }
            return str;
        }
    }
}
