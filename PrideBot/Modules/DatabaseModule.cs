using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using System.Net;
using Microsoft.Data.SqlClient;
using PrideBot.Models;
using PrideBot.Repository;
using PrideBot.Sheets;
using PrideBot.Registration;
using PrideBot.Game;
using PrideBot.Events;
using System.Text.RegularExpressions;

namespace PrideBot.Modules
{
    [Name("Database")]
    [RequireSage]
    public class DatabaseModule : PrideModuleBase
    {
        readonly ModelRepository modelRepository;
        readonly GoogleSheetsService sheetsService;
        readonly DialogueDict dialogueDict;
        readonly ModelRepository repo;
        readonly UserRegisteredCache regCache;
        readonly ChatScoringService chatScoringService;
        readonly AnnouncementService announcementService;
        readonly IConfigurationRoot config;

        public DatabaseModule(ModelRepository modelRepository, GoogleSheetsService sheetsService, DialogueDict dialogueDict, ModelRepository repo, UserRegisteredCache regCache, ChatScoringService chatScoringService, AnnouncementService announcementService, IConfigurationRoot config)
        {
            this.modelRepository = modelRepository;
            this.sheetsService = sheetsService;
            this.dialogueDict = dialogueDict;
            this.repo = repo;
            this.regCache = regCache;
            this.chatScoringService = chatScoringService;
            this.announcementService = announcementService;
            this.config = config;
        }

        [Command("updatetable")]
        [Alias("updatesheet", "pushtable", "pushsheet")]
        public async Task UploadSheet(string url, string tableName, bool clearContentsFirst = false)

        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var mainRowsAffected = await PushSheetAsync(url, tableName, connection, clearContentsFirst);

            if (mainRowsAffected == -1)
                await ReplyResultAsync($"No rows need changing or adding in my database.");
            else
                await ReplyResultAsync($"{mainRowsAffected} row(s) affected in my database.");

            if (!string.IsNullOrWhiteSpace(repo.GetAltConnectionString()))
            {
                using var altConnection = repo.GetAltDatabaseConnection();
                await altConnection.OpenAsync();
                var altRowsAffected = await PushSheetAsync(url, tableName, altConnection, clearContentsFirst);
                if (altRowsAffected == -1)
                    await ReplyResultAsync($"No rows need changing or adding in the other database.");
                else
                    await ReplyResultAsync($"{altRowsAffected} row(s) affected in the other database.");
            }

            await ReplyResultAsync($"Done!");
        }

        public async Task<int> PushSheetAsync(string url, string tableName, SqlConnection connection, bool clearFirst)
        {
            if (clearFirst)
                await new SqlCommand($"delete from {tableName};", connection).ExecuteNonQueryAsync();

            var idPattern = "\\/d\\/(.*?)\\/(|$)";
            var idMatch = new Regex(idPattern).Match(url);
            var groups = idMatch.Groups.Cast<Group>();
            var sheetId = groups.SkipLast(1).Last().Value + groups.Last().Value;

            var sheetsResult = await sheetsService.ReadSheetDataAsync(sheetId, "A1:Y1001");
            var fieldsRow = sheetsResult.Values.FirstOrDefault();
            var fieldDict = new Dictionary<string, Type>();


            // Remove () columns
            var notesColumns = fieldsRow
                .Where(a => a.ToString().StartsWith("("))
                .Select(a => fieldsRow.IndexOf(a))
                .ToList();
            for (int i = sheetsResult.Values.Count - 1; i >= 0; i--)
            {
                var row = sheetsResult.Values[i];
                for (int j = notesColumns.Count - 1; j >= 0; j--)
                {
                    var notesColumn = notesColumns[j];
                    if (notesColumn < row.Count)
                        row.RemoveAt(notesColumn);
                }
            }
            fieldDict = new Dictionary<string, Type>();

            // Get names and types
            foreach (var field in fieldsRow)
            {
                var fullName = field.ToString();
                var fieldName = fullName.Split('(')[0].Trim();
                fieldName = DatabaseHelper.SheetsNameToSql(fieldName);
                var typeName = fullName.Split('(').Length == 1 ? "string" : fullName.Split('(', ')')[1];
                if (typeName.Equals("int", StringComparison.OrdinalIgnoreCase))
                    typeName = "int32";
                typeName = typeName.CapitalizeFirst();
                var type = Type.GetType("System." + typeName);
                fieldDict[fieldName] = type;
            }

            // get primary keys
            var primaryKeyQuery = "SELECT column_name" +
                "\nFROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE" +
                "\nWHERE OBJECTPROPERTY(OBJECT_ID(constraint_name), 'IsPrimaryKey') = 1" +
                $"\nAND table_name = '{tableName}'";
            using var pkeyReader = await new SqlCommand(primaryKeyQuery, connection).ExecuteReaderAsync();
            var primaryKeyFields = new List<string>();
            while (pkeyReader.Read())
            {
                primaryKeyFields.Add(pkeyReader[0].ToString());
            }
            pkeyReader.Close();

            // Get the existing table data and track changesin update command
            using var reader = await new SqlCommand($"select * from {tableName}", connection).ExecuteReaderAsync();
            var fieldNames = fieldDict.Keys.ToList();
            var query = "";
            var parameterDict = new Dictionary<string, object>();
            var paramIndex = 0;
            var newRows = sheetsResult.Values.Skip(1).ToList();
            while (reader.Read())
            {
                // Find sheet row that matches based on primary key(s), if any
                var primaryKeys = new Dictionary<string, object>();
                IList<object> matchingRow = null;
                foreach (var row in sheetsResult.Values.Skip(1))
                {
                    var isMatch = true;
                    for (int i = 0; i < row.Count; i++)
                    {
                        if (primaryKeyFields.Contains(fieldNames[i]))
                        {
                            primaryKeys[fieldNames[i]] = reader[i];
                            if (!row[i].ToString().Equals(reader[i].ToString()))
                            {
                                isMatch = false;
                                break;
                            }
                        }
                    }
                    if (isMatch)
                    {
                        matchingRow = row;
                        break;
                    }
                }
                if (matchingRow == null)
                    continue;
                newRows.Remove(matchingRow);

                // Scrub for changed fields in row
                var changes = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader[0].Equals("GROUP_WATCH"))
                    {

                    }
                    var fieldName = reader.GetName(i);
                    var j = fieldNames.IndexOf(fieldName);
                    var fieldMatch = (j >= 0 && j < matchingRow.Count) ? matchingRow[j] : null;
                    if (fieldMatch == null)
                        continue;
                    var row = reader[0].ToString();
                    var b = reader[i].ToString();
                    try
                    {
                    if (!fieldMatch.ToString().Equals(reader[i].ToString()))
                        changes[fieldName] = Convert.ChangeType(fieldMatch, fieldDict[fieldName]);

                    }
                    catch
                    {

                    }
                }

                // Apply changes to update statement
                if (changes.Any())
                {
                    query += $" update {tableName} set ";
                    foreach (var change in changes)
                    {
                        if (!change.Equals(changes.First()))
                            query += ", ";
                        query += $"{change.Key} = @PARAM{paramIndex}";
                        parameterDict[$"PARAM{paramIndex++}"] = change.Value;
                    }
                    query += $" where ";
                    foreach (var pKey in primaryKeys)
                    {
                        if (!pKey.Equals(primaryKeys.First()))
                            query += " AND ";
                        var dataTest = pKey.Value;
                        query += $"{pKey.Key} = @PARAM{paramIndex}";
                        parameterDict[$"PARAM{paramIndex++}"] = pKey.Value;
                    }
                    query += ";";

                }
            }
            reader.Close();

            // Add insert new rows into the query
            var columnCount = sheetsResult.Values.Max(a => a.Count);
            foreach (var newRow in newRows)
            {
                query += $" insert into {tableName} (";
                for (int i = 0; i < columnCount; i++)
                {
                    if (i > 0)
                        query += ", ";
                    query += fieldNames[i];
                }
                query += ") values (";
                for (int i = 0; i < columnCount; i++)
                {
                    if (i > 0)
                        query += ", ";
                    query += $"@PARAM{paramIndex}";
                    parameterDict[$"PARAM{paramIndex++}"] = (i < newRow.Count ? newRow[i] : null) ?? "";
                }
                query += ");";
            }

            // Report if none need changing
            if (string.IsNullOrWhiteSpace(query))
            {
                return -1;
            }

            // Now push!!
            var command = new SqlCommand(query, connection);
            foreach (var param in parameterDict.Reverse())
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
                query = query.Replace("@" + param.Key, $"'{param.Value.ToString()}'");
            }
            return await command.ExecuteNonQueryAsync();
        }

        [Command("updatedialogue")]
        [Alias("pushdialogue")]
        public async Task UpdateDialogue()
        {
            await UploadSheet("https://docs.google.com/spreadsheets/d/14EGqZ5_gVpqRgNCjqbYXncwHrbNOjh9J-QjyrN0vJlY/edit#gid=0", "DIALOGUE");
            await dialogueDict.PullDialogueAsync();
            await ReplyAsync("Please use the command on my other self as well, so that cute devil gets the dialogue re-cached too~");
        }

        [Command("randomdata")]
        public async Task RandomShips(int userCount, int scoreCount, int seed = 0)
        {
            using var typingState = Context.Channel.EnterTypingState();
            var rand = seed == 0 ? new Random() : new Random(seed);
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var allChars = (await repo.GetAllCharactersAsync(connection)).ToList();
            var achievements = (await repo.GetAllAchievementsAsync(connection)).ToList();
            for (int userId = 0; userId < userCount; userId++)
            {
                var dbUser = await repo.GetOrCreateUserAsync(connection, userId.ToString());
                var previousShips = new List<string>();
                for (int tier = 0; tier < 3; tier++)
                {
                    if (tier > 0 && rand.NextDouble() > .75)
                        continue;
                    var char1 = allChars[GetWeightedIndex(allChars.Count, .2, rand)];
                    var char2 = allChars[GetWeightedIndex(allChars.Count, .2, rand)];
                    var dbShipId = await repo.GetOrCreateShipAsync(connection, char1.CharacterId, char2.CharacterId);
                    if (!previousShips.Contains(dbShipId))
                    {
                        await repo.CreateOrReplaceUserShip(connection, dbUser.UserId, (UserShipTier)tier, dbShipId);
                        previousShips.Add(dbShipId);
                    }
                }
                dbUser.ShipsSelected = true;
                await repo.UpdateUserAsync(connection, dbUser);
                var achievement = achievements.FirstOrDefault(a => a.AchievementId.Equals("PREREGISTER"));
                await repo.AttemptAddScoreAsync(connection, userId.ToString(), achievement.AchievementId, achievement.DefaultScore, Context.Client.CurrentUser.Id.ToString(), true);
            }

            for (int i = 0; i < scoreCount; i++)
            {
                var userId = GetWeightedIndex(userCount, .05, rand);
                var achievement = achievements[rand.Next() % achievements.Count];
                await repo.AttemptAddScoreAsync(connection, userId.ToString(), achievement.AchievementId, achievement.DefaultScore, Context.Client.CurrentUser.Id.ToString(), true);
            }

            await ReplyAsync("***WOP!*** I pulled a collection of info from a contest in another reality, just for you bestie!");
        }

        [Command("clearrandomdata")]
        public async Task RandomShips()
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var query = $"delete from ship_scores where SCORE_ID in (select SCORE_ID from VI_SCORES where len(USER_ID) < 10); "
                + $"delete from SCORES where len(USER_ID) < 5; "
                + $"delete from USER_SHIPS where len(USER_ID) < 5; "
                + $"delete from USERS where len(USER_ID) < 5; ";

            await new SqlCommand(query, connection).ExecuteNonQueryAsync();

            await ReplyAsync("***FOOP!*** I sent that data back to the universe from which it came~");
        }

        int GetWeightedIndex(int upperBoundExclusive, double individualProb, Random rand)
        {
            for (int i = 0; i < upperBoundExclusive; i++)
            {
                if (rand.NextDouble() < individualProb)
                    return i;
            }
            return rand.Next(rand.Next() % upperBoundExclusive);
        }

        [Command("updaterules")]
        [RequireGyn]
        [Summary("Updates the rules channel and auto-pushes dialogue.")]
        public async Task UpdateRules()
        {
            await ReplyAsync("Pushing dialogue...");
            await UpdateDialogue();
            await ReplyAsync("Updating rules page...");
            await announcementService.UpdateRulesAsync(Context.Guild);
            await ReplyResultAsync("Done!");
        }

        [Command("cleardbcaches")]
        [Alias("cleardbcache")]
        [Summary("Clears or refreshes certain cached database data, including dialogue")]
        public async Task ClearCaches()
        {
            regCache.Clear();
            chatScoringService.ChatData.Clear();
            await dialogueDict.PullDialogueAsync();
            await ReplyResultAsync("Done!");
        }

        [Command("setannouncementtimes")]
        public async Task SetBullshitTimes()
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var bullshits = await repo.GetAllBullshitsAsync(connection);
            var rand = new Random();
            var startTime = DateTime.Now.AddHours(1);
            var totalMinutes = (int)(new DateTime(DateTime.Now.Year, int.Parse(config["eventmonth"]) + 1, 1)
                - startTime).TotalMinutes;
            foreach (var bullshit in bullshits.Where(a => !a.Announced && (a.AnnounceTime == null || a.AnnounceTime < DateTime.Now.AddMonths(-1))))
            {
                var minutes = rand.Next(totalMinutes);
                bullshit.AnnounceTime = startTime.AddMinutes(minutes);
                await repo.UpdateBullshitAsync(connection, bullshit);
            }
            await ReplyAsync("Done!");
        }
    }
}