using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PrideBot
{
    public class MemoryFile
    {
        public string FileName { get; private set; }
        public MemoryStream Stream { get; private set; }

        public MemoryFile(MemoryStream stream, string fileName = null)
        {
            Stream = stream;
            FileName = fileName;
        }

        public MemoryFile(byte[] bytes, string fileName = null)
        {
            Stream = new MemoryStream(bytes);
            FileName = fileName;
        }
    }
}
