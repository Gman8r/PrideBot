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
using PrideBot.Sheets;
using System.Text.RegularExpressions;

namespace PrideBot.Modules
{
    [Name("Database")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class DatabaseModule : PrideModuleBase
    {
        readonly ModelRepository modelRepository;
        readonly GoogleSheetsService sheetsService;
        readonly DialogueDict dialogueDict;

        public DatabaseModule(ModelRepository modelRepository, GoogleSheetsService sheetsService, DialogueDict dialogueDict)
        {
            this.modelRepository = modelRepository;
            this.sheetsService = sheetsService;
            this.dialogueDict = dialogueDict;
        }

        [Command("updatesheet")]
        [Alias("pushsheet")]
        public async Task UploadSheet(string url, string tableName)
        {
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

            using var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();

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
                    var fieldMatch = j < matchingRow.Count ? matchingRow[j] : null;
                    if (fieldMatch == null)
                        continue;
                    var row = reader[0].ToString();
                    var b = reader[i].ToString();
                    if (!fieldMatch.ToString().Equals(reader[i].ToString()))
                        changes[fieldName] = Convert.ChangeType(fieldMatch, fieldDict[fieldName]);
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
                await ReplyResultAsync($"No rows need changing or adding.");
                return;
            }

            // Now push!!
            var command = new SqlCommand(query, connection);
            foreach (var param in parameterDict)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
                query = query.Replace("@" + param.Key, $"'{param.Value.ToString()}'");
            }
            var rowsAffected = await command.ExecuteNonQueryAsync();

            await ReplyResultAsync($"Done! {rowsAffected} row(s) affected.");

        }
        [Command("updatedialogue")]
        [Alias("pushdialogue")]
        public async Task UpdateDialogue()
        {
            await UploadSheet("https://docs.google.com/spreadsheets/d/14EGqZ5_gVpqRgNCjqbYXncwHrbNOjh9J-QjyrN0vJlY/edit#gid=0", "DIALOGUE");
            await dialogueDict.PullDialogueAsync();
        }
    }
}