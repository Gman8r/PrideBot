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
            try
            {
                return string.Format(instance.dict[key], args);
            }
            catch
            {
                return "(MISSING DIALOGUE OH NO MY BRAIN)";
            }
        } 
    }
}
