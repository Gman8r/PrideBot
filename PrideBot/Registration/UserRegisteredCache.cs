using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using PrideBot.Repository;

namespace PrideBot.Registration
{
    public class UserRegisteredCache : IDictionary<string, bool>
    {
        readonly Dictionary<string, bool> dict;
        readonly ModelRepository repo;

        public UserRegisteredCache(ModelRepository repo)
        {
            dict = new Dictionary<string, bool>();
            this.repo = repo;
        }

        public async Task<bool> GetOrDownload(string userId)
        {
            if (dict.ContainsKey(userId))
                return dict[userId];
            using var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();
            var user = await repo.GetOrCreateUserAsync(connection, userId);
            dict[userId] = user.ShipsSelected;
            return user.ShipsSelected;
        }

        public bool this[string key] { get => dict[key]; set => dict[key] = value; }

        bool IDictionary<string, bool>.this[string key] { get => dict[key]; set => dict[key] = value; }

        public ICollection<string> Keys => dict.Keys;

        public ICollection<bool> Values => dict.Values;

        public int Count => dict.Count;

        public bool IsReadOnly => false;

        public void Add(string key, bool value) => dict.Add(key, value);

        public void Add(KeyValuePair<string, bool> item) => dict[item.Key] = item.Value;

        public void Clear() => dict.Clear();

        public bool Contains(KeyValuePair<string, bool> item) => dict.ContainsKey(item.Key) && dict[item.Key].Equals(item.Value);
        public bool ContainsKey(string key) => dict.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, bool>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<string, bool>> GetEnumerator() => dict.GetEnumerator();

        public bool Remove(string key) => dict.Remove(key);

        public bool Remove(KeyValuePair<string, bool> item)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out bool value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() => dict.GetEnumerator();
    }
}
