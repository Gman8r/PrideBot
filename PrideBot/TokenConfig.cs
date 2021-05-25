using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace PrideBot
{
    public class TokenConfig
    {
        private IConfigurationRoot data;

        public TokenConfig()
        {
            var debugMode = bool.Parse(File.ReadAllText("debugmode.txt"));
            var configPath = debugMode ? "tokensdebug.yml" : "tokens.yml";
            var builder = new ConfigurationBuilder()        // Create a new instance of the config builder
                .SetBasePath(AppContext.BaseDirectory)      // Specify the default location for the config file
                .AddYamlFile(configPath);                // Add this (yaml encoded) file to the configuration
            data = builder.Build();                // Build the configuration
        }

        public string this[string key] { get => data[key]; }

        public IEnumerable<IConfigurationProvider> Providers => throw new NotImplementedException();

        public IEnumerable<IConfigurationSection> GetChildren() => data.GetChildren();

        public IConfigurationSection GetSection(string key) => data.GetSection(key);

        public void Reload() => data.Reload();
    }
}
