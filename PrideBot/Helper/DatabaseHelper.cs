using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    public static class DatabaseHelper
    {
        public static string GetConnectionString()
        {
            return File.ReadAllText("connectionstring.txt");
        }

        public static SqlConnection GetDatabaseConnection()
            => new SqlConnection(GetConnectionString());


        public static IEnumerable<T> As<T>(this SqlDataReader reader) where T : new()
        {
            var results = new List<T>();
            while(reader.Read())
            {
                results.Add(((IDataRecord)reader).As<T>());
            }
            reader.Close();
            return results;
        }

        public static T As<T>(this IDataRecord record) where T : new()
        {
            var result = new T();
            var properties = typeof(T).GetProperties();
            for (int i = 0; i < record.FieldCount; i++)
            {
                var fieldName = record.GetName(i);
                var matchingName = SqlNameToCamelCase(fieldName);
                var property = properties.FirstOrDefault(a => a.Name.Equals(matchingName, StringComparison.OrdinalIgnoreCase));
                if (property == null)
                    throw new Exception($"No field found with name {matchingName} derived from {fieldName}.");
                var value = record.IsDBNull(i) ? null : record[i];
                property.SetValue(result, value);
            }
            return result;
        }

        public static SqlCommand GetInsertCommand<T>(SqlConnection conn, T obj, string tableName)
        {
            var fields = new Dictionary<string, object>();
            foreach (var property in typeof(T).GetProperties())
            {
                if (!property.CustomAttributes.Any(a => a.AttributeType == typeof(DontPushToDatabaseAttribute)))
                    fields[CamelCaseNameToSql(property.Name)] = property.GetValue(obj);
            }

            var query = $"insert into dbo.{tableName} ({string.Join(",", fields.Keys)})" +
                $" values ({string.Join(",", fields.Keys.Select(a => "@" + a))})";
            var command = new SqlCommand(query, conn);

            foreach (var field in fields)
            {
                command.Parameters.AddWithValue("@" + field.Key, field.Value);
            }

            return command;
        }

        public static SqlCommand GetUpdateCommand<T>(SqlConnection conn, T obj, string tableName)
        {
            var fields = new Dictionary<string, object>();
            KeyValuePair<string, object> primaryKey = new KeyValuePair<string, object>(null, null);
            foreach (var property in typeof(T).GetProperties())
            {
                if (property.CustomAttributes.Any(a => a.AttributeType == typeof(PrimaryKeyAttribute)))
                    primaryKey = new KeyValuePair<string, object>(CamelCaseNameToSql(property.Name), property.GetValue(obj));
                else if (!property.CustomAttributes.Any(a => a.AttributeType == typeof(DontPushToDatabaseAttribute)))
                    fields[CamelCaseNameToSql(property.Name)] = property.GetValue(obj);
            }

            var query = $"update dbo.{tableName} set {string.Join(" , ", fields.Select(a => $"{a.Key} = @{a.Key}"))} where {primaryKey.Key} = {primaryKey.Value}";
            var command = new SqlCommand(query, conn);

            foreach (var field in fields)
            {
                command.Parameters.AddWithValue("@" + field.Key, field.Value);
            }

            return command;
        }

        public static string SqlNameToCamelCase(string sqlName)
        {
            var name = sqlName.Substring(0, 1).ToLower();
            for (int i = 1; i < sqlName.Length; i++)
            {
                if (sqlName[i] == '_')
                {
                    name += sqlName[i + 1];
                    i++;
                }
                else
                    name += sqlName[i].ToString().ToLower();
            }
            return name;
        }

        public static string SheetsNameToSql(string sheetsName)
            => sheetsName.ToUpper().Replace(" ", "_");

        public static string CamelCaseNameToSql(string camelName)
            => StringHelper.CamelCaseSpaces(camelName).Replace(" ", "_").ToUpper();
    }
}
