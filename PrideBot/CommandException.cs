using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    class CommandException : Exception
    {
        public CommandException(string errorMessage, Exception innerException = null) : base(errorMessage, innerException)
        {

        }
    }
}
