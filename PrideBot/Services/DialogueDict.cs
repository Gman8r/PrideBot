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
            var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();
            var results = await repo.GetAllDialogueAsync(connection);
            dict = results.ToDictionary(t => t.DialogueId, t => t.Content);
        }

        public static string Get(string key, params object[] args)
        {
            if (!instance.dict.ContainsKey(key))
                return "(MISSING DIALOGUE OH NO MY BRAIN)";

            var str = string.Format(instance.dict[key]
                .Replace("{SP}", EmoteHelper.SPEmote.ToString())
                , args);
            return RollBullshit(str);
        } 

        public static string RollBullshit(string str)
        {
            var rand = new Random();

            var bullshitPhrases = new List<string>() {
                    "Wowwwwwww.",
                    "SJFKJFsg.",
                    "Fortnite.",
                    "Minecraft.",
                    "Buy Skyrim.",
                    "Gay.",
                    "Gayyy.",
                    "God I'm cute.",
                    "Gamer.",
                    "ASFJHAJSH!!",
                    "KSHGSJVVH.",
                    "PFPShfhfh.",
                    "AAAkfsjkfsfdjsjs.",
                    "Gender??",
                    "Gay rights!",
                    "Trans rights!",
                    "ACAB!",
                    "Worm.",
                    "Like comment and subscribe.",
                    "Brainrot got me like.",
                    "On god?",
                    "Yooooo.",
                    "pspspsp here kitty.",
                    "Where the hell am I?",
                    "Tsuchinoko real.",
                    "I love women.",
                    "I want money.",
                    "Women.",
                    "Miku Hatsune.",
                    "Daaaaamn.",
                    "Fuck!",
                    "Lasers!!",
                    "Home of sexual.",
                    "Gay Gay Homo Sexual Gay."};

            var roll = rand.Next() % 25;
            if (str.Length > 0 && char.IsPunctuation(str.Last()) && roll == 0)
                str += " " + bullshitPhrases[rand.Next() % bullshitPhrases.Count];
            return str;
        }
    }
}
