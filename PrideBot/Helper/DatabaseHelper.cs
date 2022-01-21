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
                    continue;
                //throw new Exception($"No field found with name {matchingName} derived from {fieldName}.");
                object value;
                if (property.PropertyType == typeof(bool))
                    value = (record[i] ?? "N").Equals("Y");
                else
                    value = record.IsDBNull(i) ? null : record[i];
                try
                {

                property.SetValue(result, value);
                }
                catch (Exception e)
                {
                    var aasf = 1;
                }
            }
            return result;
        }

        public static SqlCommand GetInsertCommand<T>(SqlConnection conn, T obj, string tableName)
        {
            var fields = new Dictionary<string, object>();
            foreach (var property in typeof(T).GetProperties())
            {
                if (typeof(T).BaseType != null)
                {
                    // Ignore superclass when pushing
                    if (typeof(T).BaseType.GetProperties().Contains(property))
                        continue;
                }
                var key = CamelCaseNameToSql(property.Name);
                var value = property.GetValue(obj);
                if (value != null && value.GetType() == typeof(bool))
                    value = (bool)value ? "Y" : "N";
                if (!property.CustomAttributes.Any(a => a.AttributeType == typeof(DontPushToDatabaseAttribute)))
                    fields[key] = value;

            }

            var query = $"insert into dbo.{tableName} ({string.Join(",", fields.Keys)})" +
                $" values ({string.Join(",", fields.Select(a => (a.Value != null ? ("@" + a.Key) : "null")))})";
            var command = new SqlCommand(query, conn);

            foreach (var field in fields.Where(a => a.Value != null))
            {
                command.Parameters.AddWithValue("@" + field.Key, field.Value);
            }

            return command;
        }

        public static SqlCommand GetUpdateCommand<T>(SqlConnection conn, T obj, string tableName)
        {
            var fields = new Dictionary<string, object>();
            var primaryKeys = new Dictionary<string, object>();
            foreach (var property in typeof(T).GetProperties())
            {
                var key = CamelCaseNameToSql(property.Name);
                var value = property.GetValue(obj);
                if (value != null && value.GetType() == typeof(bool))
                    value = (bool)value ? "Y" : "N";
                if (property.CustomAttributes.Any(a => a.AttributeType == typeof(PrimaryKeyAttribute)))
                    primaryKeys[key] = value;
                if (typeof(T).BaseType != null)
                {
                    // Ignore superclass when pushing
                    if (typeof(T).BaseType.GetProperties().Contains(property))
                        continue;
                }
                if (!property.CustomAttributes.Any(a => a.AttributeType == typeof(DontPushToDatabaseAttribute)))
                    fields[key] = value;
            }

            var query = $"update dbo.{tableName} set {string.Join(" , ", fields.Select(a => $"{a.Key} = {(a.Value != null ? ($"@{a.Key}F") : "null")}"))}" +
                $" where {string.Join(" and ", primaryKeys.Select(a => $"{a.Key} = {(a.Value != null ? ($"@{a.Key}K") : "null")}"))}";
            var command = new SqlCommand(query, conn);

            foreach (var field in fields.Where(a => a.Value != null))
            {
                command.Parameters.AddWithValue($"@{field.Key}F", field.Value);
                query = query.Replace($"@{field.Key}F", field.Value.ToString());
            }
            foreach (var key in primaryKeys.Where(a => a.Value != null))
            {
                command.Parameters.AddWithValue($"@{key.Key}K", key.Value);
                query = query.Replace($"@{key.Key}K", key.Value.ToString());
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
