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
    }
}
