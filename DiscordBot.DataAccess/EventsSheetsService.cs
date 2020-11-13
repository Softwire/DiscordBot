using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google;
using DiscordBot.DataAccess.Models;
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
        Task EditEventAsync(
            int eventKey,
            string? description = null,
            string? name = null,
            DateTime? time = null
        );
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

        private readonly SheetsColumn KeyColumn = new SheetsColumn(0);
        private readonly SheetsColumn NameColumn = new SheetsColumn(1);
        private readonly SheetsColumn DescriptionColumn = new SheetsColumn(2);
        private readonly SheetsColumn TimeColumn = new SheetsColumn(3);
        private readonly SheetsColumn LocationColumn = new SheetsColumn(4);

        public EventsSheetsService()
        {
            var credential = GetCredential();

            sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName
            });

            largestKey = GetLargestKey();
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
                $"{KeyColumn.Letter}:{LocationColumn.Letter}"
            );
            request.ValueInputOption = AppendRequest.ValueInputOptionEnum.RAW;

            await request.ExecuteAsync();
        }

        public async Task EditEventAsync(
            int eventKey,
            string? description = null,
            string? name = null,
            DateTime? time = null
        )
        {
            var rowNumber = await GetEventRowNumber(eventKey);

            var data = new List<ValueRange>();

            if (name != null)
            {
                data.Add(MakeCellUpdate($"EventsMetadata!{NameColumn.Letter}{rowNumber}", name));
            }

            if (description != null)
            {
                data.Add(MakeCellUpdate($"EventsMetadata!{DescriptionColumn}{rowNumber}", description));
            }

            if (time != null)
            {
                data.Add(MakeCellUpdate(
                    $"EventsMetadata!{TimeColumn.Letter}{rowNumber}",
                    time.Value.ToString("s")
                ));
            }

            if (!data.Any())
            {
                return;
            }

            var updateRequest = new BatchUpdateValuesRequest()
            {
                ValueInputOption = "RAW",
                Data = data
            };

            var request = sheetsService.Spreadsheets.Values.BatchUpdate(updateRequest, spreadsheetId);
            await request.ExecuteAsync();
        }

        public async Task RemoveEventAsync(int key)
        {
        }

        public async Task<DiscordEvent> GetEventAsync(int eventKey)
        {
            var discordEvents = await ListEventsAsync();

            var result = discordEvents.FirstOrDefault(discordEvent => discordEvent.Key == eventKey);
            if (result == null)
            {
                throw new EventNotFoundException($"Event key {eventKey} not recognised");
            }

            return result;
        }

        public async Task<IEnumerable<DiscordEvent>> ListEventsAsync()
        {
            try
            {
                var request = sheetsService.Spreadsheets.Values.Get(
                    spreadsheetId,
                    $"EventsMetadata!{KeyColumn.Letter}:{LocationColumn.Letter}"
                );
                request.ValueRenderOption = GetRequest.ValueRenderOptionEnum.FORMATTEDVALUE;
                var response = await request.ExecuteAsync();

                if (response == null || response.Values.Count < 1)
                {
                    throw new EventsSheetsInitialisationException("Metadata sheet is empty");
                }

                return response.Values
                    .Skip(1) // Skip header row
                    .Select(row => new DiscordEvent(
                        (string) row[NameColumn.Index],
                        (string) row[DescriptionColumn.Index],
                        int.Parse((string) row[KeyColumn.Index]),
                        (DateTime) row[TimeColumn.Index]
                    ));
            }
            catch (GoogleApiException exception)
            {
                throw new EventsSheetsInitialisationException(
                    "Events Sheets Service couldn't initialise",
                    exception
                );
            }
        }

        private static ServiceAccountCredential GetCredential(string path = "credentials.json")
        {
            using var stream =
                new FileStream(path, FileMode.Open, FileAccess.Read);

            return GoogleCredential.FromStream(stream)
                       .CreateScoped(scopes)
                       .UnderlyingCredential as ServiceAccountCredential
                   ?? throw new EventsSheetsInitialisationException("Credential maker returned null");
        }

        private int GetLargestKey()
        {
            try
            {
                var request = sheetsService.Spreadsheets.Values.Get(
                    spreadsheetId,
                    $"EventsMetadata!{KeyColumn.Letter}:{KeyColumn.Letter}"
                );
                var response = request.Execute();

                if (response == null || response.Values.Count < 1)
                {
                    throw new EventsSheetsInitialisationException("Metadata sheet is empty");
                }

                // If the table only contains headers, and no data
                if (response.Values.Count == 1)
                {
                    return 0;
                }

                return int.Parse((string)response.Values.Skip(1).Last()[KeyColumn.Index]);
            }
            catch (GoogleApiException exception)
            {
                throw new EventsSheetsInitialisationException(
                    $"Events Sheets Service couldn't initialise",
                    exception
                );
            }
        }

        private async Task<int> GetEventRowNumber(int eventKey)
        {
            try
            {
                var request = sheetsService.Spreadsheets.Values.Get(
                    spreadsheetId,
                    $"EventsMetadata!{KeyColumn.Letter}:{KeyColumn.Letter}"
                );
                var response = await request.ExecuteAsync();

                if (response == null || response.Values.Count < 2)
                {
                    throw new EventNotFoundException($"Event key {eventKey} not recognised");
                }

                var rowNumber = response.Values
                    .Skip(1)  // Skip header row
                    .Select((values, index) => (values, index))
                    .First(row => int.Parse((string)row.values[KeyColumn.Index]) == eventKey);

                // Extract row number, plus 2 to correct for two this:
                // These lists are 0 indexed, but Sheets index from 1
                // Correct for skipping row 1, the header
                return rowNumber.index + 2;
            }
            catch (InvalidOperationException)
            {
                throw new EventNotFoundException($"Event key {eventKey} not recognised");
            }
            catch (GoogleApiException exception)
            {
                throw new EventsSheetsInitialisationException(
                    "Events Sheets Service couldn't initialise",
                    exception
                );
            }

        }

        private ValueRange MakeCellUpdate(string range, object value)
        {
            return new ValueRange()
            {
                Range = range,
                Values = new IList<object>[] { new[] { value } }
            };
        }
    }
}
