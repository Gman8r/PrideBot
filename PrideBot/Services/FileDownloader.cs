using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;

namespace PrideBot
{
    public class FileDownloader
    {
        public string FolderPath { get; }
        public bool OverwriteFiles { get; }
        public bool RedownloadFilesWithSameName { get; }
        public bool AllowLocal { get; }

        public FileDownloader(string folderPath, bool overwriteFiles = true, bool redownloadFilesWithSameName = true, bool allowLocal = false)
        {
            FolderPath = folderPath;
            OverwriteFiles = overwriteFiles;
            RedownloadFilesWithSameName = redownloadFilesWithSameName;
            AllowLocal = allowLocal;
        }

        public async Task<string> DownloadFileAsync(string url, string overrideName = "")
        {
            var segments = url.Split('/');

            string fileName = segments[segments.Length - 1];
            segments = fileName.Split('.');


            // if local file
            if (url[1].Equals(':'))
            {
                fileName = Path.GetFileNameWithoutExtension(url);
            }
            else
                fileName = segments[0];

            fileName = string.IsNullOrEmpty(overrideName) ? fileName : overrideName;
            if (!OverwriteFiles)
            {
                var initialFileName = fileName;
                int j = 1;
                while (FileExists(fileName))
                {
                    fileName = initialFileName + "-" + j.ToString();
                    j++;
                }
            }

            // if local file
            if (AllowLocal && url[1].Equals(':'))
            {
                string fileExtension = Path.GetExtension(url);
                var path = Path.Combine(FolderPath, fileName + fileExtension);
                File.Copy(url, path);
                return path;
            }
            else
            {
                using (var client = new WebClient())
                {
                    Uri uri = new Uri(url);
                    var fileInfo = new FileInfo(uri.AbsolutePath);
                    if (string.IsNullOrWhiteSpace(fileInfo.Extension))
                    {
                        throw new CommandException("Url does not lead to a file");
                    }
                    var path = FolderPath + "/" + fileName + fileInfo.Extension;
                    if (!RedownloadFilesWithSameName && File.Exists(path))
                        return path;
                    await client.DownloadFileTaskAsync(url, path);
                    return path;
                }
            }

        }

        public bool FileExists(string filename)
        {
            var file = Path.Combine(FolderPath, filename);
            return File.Exists(file);
        }
    }
}
