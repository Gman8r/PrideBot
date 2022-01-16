﻿using Discord;
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
using PrideBot.GDrive;
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
        readonly GoogleDriveService driveService;
        readonly DialogueDict dialogueDict;
        readonly ModelRepository repo;
        readonly UserRegisteredCache regCache;
        readonly ChatScoringService chatScoringService;
        readonly AnnouncementService announcementService;
        readonly IConfigurationRoot config;

        public DatabaseModule(ModelRepository modelRepository, GoogleSheetsService sheetsService, DialogueDict dialogueDict, ModelRepository repo, UserRegisteredCache regCache, ChatScoringService chatScoringService, AnnouncementService announcementService, IConfigurationRoot config, GoogleDriveService driveService)
        {
            this.modelRepository = modelRepository;
            this.sheetsService = sheetsService;
            this.dialogueDict = dialogueDict;
            this.repo = repo;
            this.regCache = regCache;
            this.chatScoringService = chatScoringService;
            this.announcementService = announcementService;
            this.config = config;
            this.driveService = driveService;
        }



        [Command("updatealltables")]
        [Summary("Big red button, very fun")]
        [Alias("updatallsheets", "pushalltables", "pushallsheets")]
        public async Task UploadAllSheets(bool clearContentsFirst = false)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var files = await driveService.GetFilesInFolderAsync(config["ids:tablesdrivefolder"]);
            foreach (var file in files)
            {
                var url = $"https://docs.google.com/spreadsheets/d/{file.Id}/edit#gid=0";
                await UploadAllSubsheets(url, clearContentsFirst);
            }
            await ReplyAsync("Done for real!!");
        }

        [Command("updatetable")]
        [Alias("updatesheet", "pushtable", "pushsheet")]
        [Summary("Updates a single subsheet from a url")]
        [Priority(1)]
        public async Task UploadSingleSubsheet(string url, bool clearContentsFirst = false)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();

            var sheetIds = GetSheetIds(url);
            var metaData = await sheetsService.GetSheetMetaDataAsync(sheetIds.Item1);
            var tableName = metaData.Properties.Title.Replace(" ", "_");
            var subsheetName = metaData.Sheets
                .FirstOrDefault(a => a.Properties.SheetId == int.Parse(sheetIds.Item2))
                .Properties.Title;

            await UploadSubsheet(connection, sheetIds, tableName, subsheetName, clearContentsFirst);
        }

        [Command("updatetablefull")]
        [Summary("Updates every subsheet from the table linked in the url")]
        [Alias("updatesheetfull", "pushtablefull", "pushsheetfull")]
        public async Task UploadAllSubsheets(string url, bool clearContentsFirst = false)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();

            var baseSheetIds = GetSheetIds(url);
            var metaData = await sheetsService.GetSheetMetaDataAsync(baseSheetIds.Item1);
            var tableName = metaData.Properties.Title.Replace(" ", "_");

            foreach (var sheet in metaData.Sheets)
            {
                await UploadSubsheet(connection, (baseSheetIds.Item1, sheet.Properties.SheetId.ToString()), tableName, sheet.Properties.Title, clearContentsFirst);
            }
        }

        private (string, string) GetSheetIds(string url)
        {
            var idPattern = @"\/d\/(.*?)\/.*gid=([^$]*)";
            var idMatch = new Regex(idPattern).Match(url);
            var groups = idMatch.Groups.Cast<Group>();
            var sheetId = groups.SkipLast(1).Last().Value;
            var subsheetId = groups.Last().Value;
            return (sheetId, subsheetId);
        }

        async Task UploadSubsheet(SqlConnection connection, (string, string) sheetIds, string tableName, string subsheetName, bool clearContentsFirst = false)
        {
            await ReplyResultAsync($"Uploading {subsheetName} in {tableName}...");

            var mainRowsAffected = await PushSheetAsync(tableName, sheetIds.Item1, subsheetName, connection, clearContentsFirst);
            if (mainRowsAffected == -1)
                await ReplyResultAsync($"No rows need changing or adding in my database.");
            else
                await ReplyResultAsync($"{mainRowsAffected} row(s) affected in my database.");

            if (!string.IsNullOrWhiteSpace(repo.GetAltConnectionString()))
            {
                using var altConnection = repo.GetAltDatabaseConnection();
                await altConnection.OpenAsync();
                var altRowsAffected = await PushSheetAsync(tableName, sheetIds.Item1, subsheetName, altConnection, clearContentsFirst);
                if (altRowsAffected == -1)
                    await ReplyResultAsync($"No rows need changing or adding in the other database.");
                else
                    await ReplyResultAsync($"{altRowsAffected} row(s) affected in the other database.");
            }

            await ReplyResultAsync($"Done!");
        }

        public async Task<int> PushSheetAsync(string tableName, string sheetId, string subsheetName, SqlConnection connection, bool clearFirst)
        {

            var sheetsResult = await sheetsService.ReadSheetDataAsync(sheetId, $"{subsheetName}!A1:Y1001");
            var fieldsRow = sheetsResult.Values.FirstOrDefault();
            var fieldDict = new Dictionary<string, Type>();

            if (clearFirst)
                await new SqlCommand($"delete from {tableName};", connection).ExecuteNonQueryAsync();

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
                object value = param.Value;
                // Nullify empty strings
                if (param.Value.GetType() == typeof(string) && string.IsNullOrEmpty((string)value))
                    value = null;
                command.Parameters.AddWithValue(param.Key, value ?? DBNull.Value);
                query = query.Replace("@" + param.Key, $"'{value ?? "null"}'");
            }
            return await command.ExecuteNonQueryAsync();
        }

        [Command("pushdialogue")]
        [Alias("updatedilogue")]
        [Priority(1)]
        public async Task PushDialogue()
        {
            await UploadAllSubsheets(@"https://docs.google.com/spreadsheets/d/14EGqZ5_gVpqRgNCjqbYXncwHrbNOjh9J-QjyrN0vJlY/edit#gid=0");
            await dialogueDict.PullDialogueAsync();
            await ReplyAsync("Dialogue updated! You'll need to repeat this command on any other instances of me that are running as well.");
        }
    }
}