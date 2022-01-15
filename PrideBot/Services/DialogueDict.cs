using Newtonsoft.Json;
using PrideBot.Models;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GroupDocs.Search;
using System.Diagnostics;

namespace PrideBot
{
    public class DialogueDict
    {
        static DialogueDict instance;

        Dictionary<string, string> dict;

        private readonly ModelRepository repo;
        private List<KeyValuePair<string, string>> emojiSuggestions;
        private List<string> stopWords;
        private GroupDocs.Search.Index wordIndex = new GroupDocs.Search.Index();

        public DialogueDict(ModelRepository repo)
        {
            this.repo = repo;

            instance = this;
            dict = new Dictionary<string, string>();
            PullDialogueAsync().GetAwaiter();
        }

        public async Task PullDialogueAsync()
        {
            try
            {
                var connection = repo.GetDatabaseConnection();
                await connection.OpenAsync();
                var results = await repo.GetAllDialogueAsync(connection);
                dict = results.ToDictionary(t => t.DialogueId, t => t.Content);

                var emojiSuggestionText = await File.ReadAllTextAsync("Services/emojisuggestions.json");
                // Split each emoji suggestion value by its delimiter ( | )
                emojiSuggestions = JsonConvert.DeserializeObject<Dictionary<string, string>>(emojiSuggestionText)
                    .SelectMany(a => a.Value.Split("|").Select(aa => (a.Key, aa.Trim())))
                    .Select(a => new KeyValuePair<string, string>(a.Item1, a.Item2))
                    .ToList();
                var additionalStopWords = await File.ReadAllLinesAsync("Services/customstopwords.txt");
                stopWords = wordIndex.Dictionaries.StopWordDictionary
                    .Where(a => !a.Any(aa => aa < 'A' || aa > 'z' || char.IsPunctuation(aa)))   // Letters only
                    .Concat(additionalStopWords
                        .Select(a => new string(a.Where(aa => char.IsLetter(aa)).ToArray())))
                    .Select(a => a.ToLower())
                    .Distinct()
                    .ToList();

                for (int i = emojiSuggestions.Count - 1; i >= 0; i--)
                {
                    var words = emojiSuggestions[i].Value
                        .Split()
                        .Where(a => !stopWords.Contains(a));
                    if (!words.Any())
                        emojiSuggestions.RemoveAt(i);
                    else
                        emojiSuggestions[i] = new KeyValuePair<string, string>(emojiSuggestions[i].Key, string.Join(" ", words));
                }
            }
            catch(Exception e)
            {
                // TODO log
            }
        }

        public static Dictionary<string, string> GetDict() => instance.dict;


        public static string GetNoBrainRot(string key, params object[] args)
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
                return $"MISSING DIALOGUE OH NO MY BRAIN ({key})";

            var str = string.Format(instance.dict[chosenKey]
                .Replace("{SP}", EmoteHelper.SPEmote.ToString())
                , args);
            return str;
        }

        public static string Get(string key, params object[] args) => GenerateEmojiText(GetNoBrainRot(key, args));

        public static string RollBrainrot(string str, double chance = 1.0/17.0, Random rand = null, bool isRecursing = false)
        {
            rand ??= new Random();

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
                    "Mom holy fuck!",
                    "Skadoosh!",
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
                var recursiveChance = isRecursing ? chance - .1 : .25;
                str = RollBrainrot(str, recursiveChance, rand, true);    // Double up baby
            }
            return str;
        }

        public static string GenerateEmojiText(string text) => instance.GenerateEmojiTextInternal(text);

        public string GenerateEmojiTextInternal(string text)
        {
            var returnText = "";
            var wordChance = 0.0;
            var rand = new Random();
            var lastEmojiAt = 0;
            text += " ";    // Space buffer at the end so last word searches for emoji as well
            for (int i = 0; i < text.Length; i++)
            {
                var chr = text[i];
                var textSinceLastEmoji = text.Substring(lastEmojiAt, i - lastEmojiAt);
                if (i > 0
                    && char.IsWhiteSpace(chr)
                    && !char.IsWhiteSpace(text[i-1]))
                {
                    wordChance += .1;
                    var nextChance = rand.NextDouble();
                    if (nextChance < wordChance)
                    {
                        var suggestions = GetEmojiSuggestions(textSinceLastEmoji.Split().Last());
                        if (suggestions.Any())
                        {
                            //var maxScore = suggestions.Max(a => a.Value);
                            //var maxScoreSuggestions = suggestions
                            //    .Where(a => a.Value == maxScore)
                            //    .ToList();
                            var chosenSuggestion = suggestions[rand.Next(suggestions.Count)];
                            returnText += " " + chosenSuggestion;
                            wordChance -= 1.0;
                            lastEmojiAt = i;
                        }
                    }
                }
                
                // Don't include extra space we added
                if (i < text.Length - 1)
                    returnText += chr;
            }
            return returnText;
        }

        // <emoji, score for how many words match>
        public List<string> GetEmojiSuggestions(string word)
        {
            // remove non-letter chars
            //phrase = new string(phrase.Where(a => char.IsLetter(a) || char.IsWhiteSpace(a)).ToArray()).ToLower();
            //var words = phrase.Split()
            //    .Select(a => a.ToLower())
            //    .ToList();
            word = new string(word.Where(a => char.IsLetter(a) || char.IsWhiteSpace(a)).ToArray()).ToLower();
            if (stopWords.Contains(word))
                return new List<string>();
            var suggestions = new List<string>();
            // Add synonyms
            var synonyms = wordIndex.Dictionaries.SynonymDictionary.GetSynonyms(word);
            //synonyms.Add(word);

            // Create emoji scores
            var emojiScores = new List<KeyValuePair<string, int>>();
            var synonymSuggestions = new List<string>();
            foreach (var suggestion in emojiSuggestions)
            {
                var emojiWords = suggestion.Value.Split();
                if (!emojiWords.Any())
                    continue;
                if (emojiWords.First().Equals(word) || emojiWords.Last().Equals(word))
                    suggestions.Add(suggestion.Key);
                if (synonyms.Any(a => emojiWords.First().Equals(a) || emojiWords.Last().Equals(a)))
                    synonymSuggestions.Add(suggestion.Key);

                // I tried a lot of dumb things but im too scared to get rid of them

                ////var sw = new Stopwatch();
                ////sw.Start();

                //// Old method ignoring word order
                ////var wordScore = words
                ////    .Count(a => emojiWords.Contains(a));
                ////var anyNonTrivial = words
                ////    .Any(a => emojiWords.Contains(a) && !wordIndex.Dictionaries.StopWordDictionary.Contains(a));
                ////wordScore += 1000 - (suggestion.Value.Split().Count() * 2);
                ////if (wordScore > 0 && anyNonTrivial)
                ////    emojiScores.Add(new KeyValuePair<string, int>(suggestion.Key, wordScore));

                //var foundNonTrivialWord = false;
                //var wordScore = 0;

                //// Create variations
                ////var variationDict = new Dictionary<string, List<String>>();
                ////foreach (var word in words.Distinct())
                ////{
                ////    var allVariations = new List<string>() { word };
                ////    //if (!stopWords.Contains(word))
                ////    //{
                ////    //    var synonyms = wordIndex.Dictionaries.SynonymDictionary.GetSynonyms(word).ToList();
                ////    //    synonyms.Add(word);
                ////    //    allVariations = synonyms;
                ////    //}
                ////    //foreach (var synonym in synonyms)
                ////    //{
                ////    //    allVariations.AddRange(wordIndex.Dictionaries.WordFormsProvider.GetWordForms(synonym));
                ////    //}
                ////    //allVariations.AddRange(synonyms);
                ////    variationDict[word] = allVariations;
                ////}

                //////Check words in backwards order to see if the end of one phrase matches the other(mindful of variations)
                ////for (int i = words.Count - 1; i >= 0; i--)
                ////{
                ////    var word = words[i].ToLower();
                ////    var i2 = emojiWords.Count - words.Count;
                ////    if (i2 < 0)
                ////        break;
                ////    if (!emojiWords[i2].Equals(word))
                ////        break;
                ////    wordScore++;
                ////    if (!stopWords.Contains(word))
                ////        foundNonTrivialWord = true;
                ////}

                //////Also match if last word matches the first word of the emoji alias
                ////if (wordScore == 0 && emojiWords.First().Equals(words.Last()) && !stopWords.Contains(emojiWords.First()))
                ////{
                ////    wordScore++;
                ////    foundNonTrivialWord = true;
                ////}

                ////Console.WriteLine(sw.ElapsedMilliseconds + " at " + "wordchecked");
                //if (foundNonTrivialWord)
                //    emojiScores.Add(new KeyValuePair<string, int>(suggestion.Key, wordScore));
            }

            //// Only take the max-score-value'd suggestion for any given emoji
            //var returnDict  = emojiScores
            //    .GroupBy(a => a.Key)
            //    .ToDictionary(k => k.Key, v => v.Max(a => a.Value));

            //return returnDict;

            if (!suggestions.Any())
                suggestions.AddRange(synonymSuggestions);
            suggestions = suggestions.Distinct().ToList();
            return suggestions;
        }
    }
}