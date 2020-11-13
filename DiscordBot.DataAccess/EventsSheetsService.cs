using System;
using System.Collections.Generic;
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
        Task AddEventAsync(
            string name,
            string description,
            DateTime time,
            IEnumerable<EventResponse>? responses = null
        );
        Task EditEventAsync(
            int eventKey,
            string? description = null,
            string? name = null,
            DateTime? time = null
        );
        Task RemoveEventAsync(int eventKey);

        Task<DiscordEvent> GetEventAsync(int eventKey);
        Task<IEnumerable<DiscordEvent>> ListEventsAsync();

        Task AddResponseForUser(int eventKey, ulong userId, string responseEmoji);
        Task ClearResponsesForUser(int eventKey, ulong userId);

        Task<Dictionary<ulong, IEnumerable<EventResponse>>> GetSignupsByUser(int eventId);
        Task<Dictionary<EventResponse, IEnumerable<ulong>>> GetSignupsByResponse(int eventId);
    }

    public class EventsSheetsService : IEventsSheetsService
    {
        private static readonly string[] scopes = { SheetsService.Scope.Spreadsheets };
        private static readonly string applicationName = "Softwire Discord Bot";
        private static readonly string? spreadsheetId = SheetsEnvironmentVariables.SheetId;

        private readonly SheetsService sheetsService;
        private readonly int metadataSheetId;
        private int largestKey;

        private const string MetadataSheetName = "EventsMetadata";
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
            metadataSheetId = GetMetadataSheetId();
        }

        public async Task AddEventAsync(
            string name,
            string description,
            DateTime time,
            IEnumerable<EventResponse>? responses = null
        )
        {
            responses ??= new[]
            {
                new EventResponse(":white_check_mark:", "Yes"),
                new EventResponse(":grey_question:", "Maybe")
            };

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
                data.Add(MakeCellUpdate($"{MetadataSheetName}!{NameColumn.Letter}{rowNumber}", name));
            }

            if (description != null)
            {
                data.Add(MakeCellUpdate($"{MetadataSheetName}!{DescriptionColumn}{rowNumber}", description));
            }

            if (time != null)
            {
                data.Add(MakeCellUpdate(
                    $"{MetadataSheetName}!{TimeColumn.Letter}{rowNumber}",
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

        public async Task RemoveEventAsync(int eventKey)
        {
            var rowNumber = await GetEventRowNumber(eventKey);

            var requestParameters = new BatchUpdateSpreadsheetRequest()
            {
                Requests = new[]
                {
                    new Request()
                    {
                        DeleteDimension = new DeleteDimensionRequest()
                        {
                            Range = new DimensionRange()
                            {
                                SheetId = metadataSheetId,
                                Dimension = "ROWS",
                                StartIndex = rowNumber - 1,
                                EndIndex = rowNumber
                            }
                        }
                    }
                }
            };

            var request = sheetsService.Spreadsheets.BatchUpdate(requestParameters, spreadsheetId);
            await request.ExecuteAsync();
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
                    $"{MetadataSheetName}!{KeyColumn.Letter}:{LocationColumn.Letter}"
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

#pragma warning disable 1998 // Disable compiler warning stating that the unimplemented functions are synchronous
        public async Task AddResponseForUser(int eventKey, ulong userId, string responseEmoji)
        {
        }

        public async Task ClearResponsesForUser(int eventKey, ulong userId)
        {
        }

        public async Task<Dictionary<ulong, IEnumerable<EventResponse>>> GetSignupsByUser(int eventId)
        {
            return new Dictionary<ulong, IEnumerable<EventResponse>>
            {
                { 0UL, new[] { new EventResponse(":white_check_mark:", "Yes") } },
                { 1UL, new[] { new EventResponse(":grey_question:", "Maybe") } }
            };
        }

        public async Task<Dictionary<EventResponse, IEnumerable<ulong>>> GetSignupsByResponse(int eventId)
        {
            return new Dictionary<EventResponse, IEnumerable<ulong>>
            {
                { new EventResponse(":white_check_mark:", "Yes"), new[] { 0UL } },
                { new EventResponse(":grey_question:", "Maybe"), new[] { 1UL } }
            };
        }
#pragma warning restore 1998

        private static ServiceAccountCredential GetCredential()
        {
            var clientEmail = SheetsEnvironmentVariables.ClientEmail;
            var privateKey = SheetsEnvironmentVariables.PrivateKey;

            var credentialInitializer = new ServiceAccountCredential.Initializer(clientEmail)
            {
                ProjectId = SheetsEnvironmentVariables.ProjectId,
                KeyId = SheetsEnvironmentVariables.PrivateKeyId,
                Scopes = scopes
            };

            var credentials = new ServiceAccountCredential(credentialInitializer.FromPrivateKey(privateKey));

            return credentials
                   ?? throw new EventsSheetsInitialisationException("Credential maker returned null");
        }

        private int GetLargestKey()
        {
            try
            {
                var request = sheetsService.Spreadsheets.Values.Get(
                    spreadsheetId,
                    $"{MetadataSheetName}!{KeyColumn.Letter}:{KeyColumn.Letter}"
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

        private int GetMetadataSheetId()
        {
            var sheets = sheetsService.Spreadsheets.Get(spreadsheetId).Execute().Sheets;
            var metadataSheet = sheets.FirstOrDefault(sheet => sheet.Properties.Title == MetadataSheetName);

            if (metadataSheet?.Properties.SheetId == null)
            {
                throw new EventsSheetsInitialisationException("Could not find metadata sheet");
            }

            return metadataSheet.Properties.SheetId.Value;
        }

        private async Task<int> GetEventRowNumber(int eventKey)
        {
            try
            {
                var request = sheetsService.Spreadsheets.Values.Get(
                    spreadsheetId,
                    $"{MetadataSheetName}!{KeyColumn.Letter}:{KeyColumn.Letter}"
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
