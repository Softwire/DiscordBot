using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using static Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource;

namespace DiscordBot.DataAccess
{
    public class EventsSheetsService
    {
        private static readonly string[] scopes = { SheetsService.Scope.Spreadsheets };
        private static readonly string applicationName = "Softwire Discord Bot";
        private static readonly string spreadsheetId = "";

        private readonly SheetsService sheetsService;

        public EventsSheetsService()
        {
            var credential = GetCredential();

            sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName
            });
        }

        public async Task ReadColumns()
        {
            var range = "A2:B27";
            var request =
                sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);

            var response = await request.ExecuteAsync();
            var values = response.Values;
            if (values == null || values.Count <= 0)
            {
                Console.WriteLine("No data found.");
                return;
            }

            Console.WriteLine("Alpha, Numeric");
            foreach (var row in values)
            {
                Console.WriteLine($"{row[0]}, {row[1]}");
            }
        }

        public async Task WriteRow()
        {
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object>
                    {
                        "Test write:",
                        1
                    }
                },
                Range = "A30:B30"
            };
            var request = sheetsService.Spreadsheets.Values.Update(valueRange, spreadsheetId, "A30:B30");
            request.ValueInputOption = UpdateRequest.ValueInputOptionEnum.RAW;

            await request.ExecuteAsync();
        }

        private static ServiceAccountCredential GetCredential(string path = "credentials.json")
        {
            using var stream =
                new FileStream(path, FileMode.Open, FileAccess.Read);

            return GoogleCredential.FromStream(stream)
                .CreateScoped(scopes)
                .UnderlyingCredential as ServiceAccountCredential;
        }
    }
}
