using Newtonsoft.Json;

using Google.Apis.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Google.Apis.Drive.v3;

namespace PrideBot.GDrive
{
    public class GoogleDriveService
    {
        DriveService service;
        public DriveService DriveService => service;
        public GoogleCredentialService googleCredentialService;

        public GoogleDriveService(GoogleCredentialService googleCredentialService)
        {
            this.googleCredentialService = googleCredentialService;

            // Create Google Drive API service.
            service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = googleCredentialService.Credential,
                ApplicationName = GoogleCredentialService.ApplicationName
            });
        }

        public async Task<IList<Google.Apis.Drive.v3.Data.File>> GetFilesInFolderAsync(string folderId)
        {
            var listRequest = service.Files.List();
            listRequest.Q = $"parents in '{folderId}'";
            return (await listRequest.ExecuteAsync()).Files;
        }

    }
}
