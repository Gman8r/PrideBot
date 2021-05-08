﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PrideBot
{
    public class LoggingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;

        private string _logDirectory { get;}
        private string _logFile => Path.Combine(_logDirectory, $"{DateTime.UtcNow.ToString("yyyy-MM-dd")}.txt");

        // DiscordSocketClient and CommandService are injected automatically from the IServiceProvider
        public LoggingService(DiscordSocketClient discord, CommandService commands)
        {
            _logDirectory = Path.Combine(AppContext.BaseDirectory, Startup.DebugMode ? "logsdebug" : "logs");
            _discord = discord;
            _commands = commands;
            
            _discord.Log += OnLogAsync;
            _commands.Log += OnLogAsync;
        }

        bool LogFileInUse = false;
        public async Task OnLogAsync(LogMessage msg)
        {
            if (!Directory.Exists(_logDirectory))     // Create the log directory if it doesn't exist
                Directory.CreateDirectory(_logDirectory);
            if (!File.Exists(_logFile))               // Create today's log file if it doesn't exist
                File.Create(_logFile).Dispose();

            var e = msg.Exception?.GetBaseException().GetBaseException();
            var errorMessage = e?.ToString() ?? msg.Message;
            if (e != null && msg.Exception.GetBaseException().GetType() == typeof(CommandException))
                errorMessage = e.Message;
            string logText = $"{DateTime.Now:HH:mm:ss} [{msg.Severity}] {msg.Source}: {errorMessage}";

            while (LogFileInUse)
                await Task.Delay(25);
            LogFileInUse = true;
            File.AppendAllText(_logFile, logText + "\n");     // Write the log text to a file
            LogFileInUse = false;

            await Console.Out.WriteLineAsync(logText);       // Write the log text to the console
        }
    }
}
