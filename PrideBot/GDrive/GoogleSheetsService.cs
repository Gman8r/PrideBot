using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Newtonsoft.Json;

using Google.Apis.Services;
using Google.Apis.Util.Store;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace PrideBot.GDrive
{
    public class GoogleSheetsService
    {
        SheetsService service;
        public SheetsService SheetsService => service;
        public GoogleCredentialService googleCredentialService;

        public GoogleSheetsService(GoogleCredentialService googleCredentialService)
        {
            this.googleCredentialService = googleCredentialService;

            // Create Google Sheets API service.
            service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = googleCredentialService.Credential,
                ApplicationName = GoogleCredentialService.ApplicationName
            });
        }

        public async Task<UpdateValuesResponse> UpdateDataAsync(string spreadsheetId, string range, IList<IList<object>> values,
            SpreadsheetsResource.ValuesResource.GetRequest.MajorDimensionEnum majorDimension = SpreadsheetsResource.ValuesResource.GetRequest.MajorDimensionEnum.ROWS)
        {
            var valueRange = new ValueRange();
            valueRange.Values = values;
            var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            return await updateRequest.ExecuteAsync();
        }

        public async Task<Spreadsheet> GetSheetMetaDataAsync(string spreadsheetId)
        {
            var dataRequest = service.Spreadsheets.Get(spreadsheetId);
            return await dataRequest.ExecuteAsync();
        }

        public async Task<ValueRange> ReadSheetDataAsync(string spreadsheetId, string range,
            SpreadsheetsResource.ValuesResource.GetRequest.MajorDimensionEnum majorDimension = SpreadsheetsResource.ValuesResource.GetRequest.MajorDimensionEnum.ROWS,
            bool flattenDimensions = false)
        {
            var valeuRequest = service.Spreadsheets.Values.Get(spreadsheetId, range);
            valeuRequest.MajorDimension = majorDimension;
            var results = await valeuRequest.ExecuteAsync();
            if (flattenDimensions)
            {
                int maxSize = results.Values.Select(a => a.Count()).Max();
                foreach (var subValues in results.Values)
                {
                    while (subValues.Count < maxSize)
                    {
                        subValues.Add(null);
                    }
                }
            }
            return results;
        }

        public async Task<SheetProperties> CopySheetAsync(string spreadsheetId, int fromId)
        {
            var copyOtherRequest = new CopySheetToAnotherSpreadsheetRequest();
            copyOtherRequest.DestinationSpreadsheetId = spreadsheetId;
            var copyRequest = service.Spreadsheets.Sheets.CopyTo(copyOtherRequest, spreadsheetId, fromId);
            
            return await copyRequest.ExecuteAsync();
        }

        public async Task<BatchUpdateSpreadsheetResponse> RenameSheetAsync(SheetProperties sheetProperties, string newName, string spreadsheetId)
        {
            var updatePropertiesRequest = new UpdateSheetPropertiesRequest();
            updatePropertiesRequest.Properties = sheetProperties;
            updatePropertiesRequest.Properties.Title = newName;
            updatePropertiesRequest.Fields = "title";
            var newRequest = new Request();
            newRequest.UpdateSheetProperties = updatePropertiesRequest;
            var batchUpdateSpreadsheetRequest = new BatchUpdateSpreadsheetRequest();
            batchUpdateSpreadsheetRequest.Requests = new List<Request>();
            batchUpdateSpreadsheetRequest.Requests.Add(newRequest);
            var batchUpdateRequest = service.Spreadsheets.BatchUpdate(batchUpdateSpreadsheetRequest, spreadsheetId);
            return await batchUpdateRequest.ExecuteAsync();
        }
    }
}
