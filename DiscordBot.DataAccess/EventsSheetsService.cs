using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscordBot.Models;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using static Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource;

namespace DiscordBot.DataAccess
{
    public interface IEventsSheetsService
    {
        Task AddEventAsync(string name, string description, DateTime time);
        Task EditEventAsync(int key, string? description = null, string? name = null, DateTime? time = null);
        Task RemoveEventAsync(int key);

        Task<DiscordEvent> GetEventAsync(int eventKey);
        Task<IEnumerable<DiscordEvent>> ListEventsAsync();
    }

    public class EventsSheetsService : IEventsSheetsService
    {
        private static readonly string[] scopes = { SheetsService.Scope.Spreadsheets };
        private static readonly string applicationName = "Softwire Discord Bot";
        private static readonly string spreadsheetId = "";

        private readonly SheetsService sheetsService;
        private int largestKey;
        private readonly Dictionary<int, int> keyToRowNumberMap;

        public EventsSheetsService()
        {
            var credential = GetCredential();

            sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName
            });

            try
            {
                var request = sheetsService.Spreadsheets.Values.Get(spreadsheetId, "EventsMetadata!A:A");
                var response = request.Execute();

                if (response == null || response.Values.Count < 1)
                {
                    throw new EventsSheetsInitialisationException("Metadata sheet is empty");
                }

                // Find which row each event is on, and the largest key in use
                keyToRowNumberMap = new Dictionary<int, int>();
                var keys = response.Values.Skip(1);
                largestKey =
                    keys
                    .Select((keyRow, rowNumber) =>
                        {
                            var key = int.Parse((string) keyRow[0]);
                            keyToRowNumberMap.Add(key, rowNumber + 1);
                            return key;
                        })
                    .Max();
            }
            catch (GoogleApiException exception)
            {
                throw new EventsSheetsInitialisationException(
                    $"Events Sheets Service couldn't initialise",
                    exception
                );
            }
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

        public async Task AddEventAsync(string name, string description, DateTime time)
        {
            // Allocate new key
            largestKey++;

            var newRow = new ValueRange
            {
                Values = new IList<object>[]
                {
                    new object[]
                    {
                        largestKey,
                        name,
                        description,
                        time.ToString("s"),
                        "Europe/London"
                    }
                }
            };

            var request = sheetsService.Spreadsheets.Values.Append(
                newRow,
                spreadsheetId,
                "A:E"
            );
            request.ValueInputOption = AppendRequest.ValueInputOptionEnum.RAW;

            var response = await request.ExecuteAsync();

            // Record which row the new event was inserted on
            keyToRowNumberMap.Add(
                largestKey,
                GetRowNumberFromRange(response.Updates.UpdatedRange)
            );
            return;
        }

        public async Task EditEventAsync(
            int key,
            string? description = null,
            string? name = null,
            DateTime? time = null)
        {
        }

        public async Task RemoveEventAsync(int key)
        {
        }

        public async Task<DiscordEvent> GetEventAsync(int eventKey)
        {
             return new DiscordEvent("Christmas Day", "Christmas!", eventKey, new DateTime(2020, 12, 25));
        }

        public async Task<IEnumerable<DiscordEvent>> ListEventsAsync()
        {
            return new[]
            {
                new DiscordEvent("Christmas Day", "Christmas!", 1, new DateTime(2020, 12, 25))
            };
        }

        private static ServiceAccountCredential GetCredential(string path = "credentials.json")
        {
            using var stream =
                new FileStream(path, FileMode.Open, FileAccess.Read);

            return GoogleCredential.FromStream(stream)
                .CreateScoped(scopes)
                .UnderlyingCredential as ServiceAccountCredential;
        }

        private int GetRowNumberFromRange(string range)
        {
            const string pattern = @"^EventsMetadata!A\d+:E(\d+)$";
            var rowString = Regex.Match(range, pattern).Groups[1].ToString();
            return int.Parse(rowString);
        }
    }
}
