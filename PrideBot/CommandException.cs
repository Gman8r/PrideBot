using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    class CommandException : Exception
    {
        public string ParsedMessage { get; }

        // We don't get passed the exception itself upon command execution, so CommandException messages have a special prefix to indicate they're not an internal error
        public CommandException(string errorMessage, Exception innerException = null, bool ephemeral = false)
            : base("COMMANDEXCEPTION:" + (ephemeral ? "EPHEMERAL:" : "") + errorMessage, innerException)
        {
            ParsedMessage = errorMessage;
        }
    }
}
