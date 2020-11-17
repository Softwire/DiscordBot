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
        Task AddMessageIdToEventAsync(int eventKey, ulong messageId);
        Task RemoveEventAsync(int eventKey);

        Task<DiscordEvent> GetEventAsync(int eventKey);
        Task<DiscordEvent> GetEventFromMessageIdAsync(ulong messageId);
        Task<IEnumerable<DiscordEvent>> ListEventsAsync();

        Task AddResponseForUserAsync(int eventKey, ulong userId, string responseEmoji);
        Task ClearResponsesForUserAsync(int eventKey, ulong userId);

        Task<Dictionary<ulong, IEnumerable<EventResponse>>> GetSignupsByUserAsync(int eventId);
        Task<Dictionary<EventResponse, IEnumerable<ulong>>> GetSignupsByResponseAsync(int eventId);
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
        private readonly SheetsColumn TimeZoneColumn = new SheetsColumn(4);
        private readonly SheetsColumn MessageIdColumn = new SheetsColumn(5);

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

            var addEventMetadata =
                SheetsServiceRequestsHelper.AddEventMetadata(metadataSheetId, largestKey, name, description, time);

            var addResponseSheet = SheetsServiceRequestsHelper.AddResponseSheet(largestKey);

            var addResponseColumns = SheetsServiceRequestsHelper.AddResponseColumns(largestKey, responses);

            var requests = new BatchUpdateSpreadsheetRequest()
            {
                Requests = new[]
                {
                    addEventMetadata,
                    addResponseSheet,
                    addResponseColumns
                }
            };

            await sheetsService.Spreadsheets.BatchUpdate(requests, spreadsheetId).ExecuteAsync();
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

        public async Task AddMessageIdToEventAsync(int eventKey, ulong messageId)
        {
            var rowNumber = await GetEventRowNumber(eventKey);

            var cellValue = new ValueRange()
            {
                Values = new IList<object>[]
                {
                    new object[] { messageId.ToString() }
                }
            };

            var request = sheetsService.Spreadsheets.Values.Update(
                cellValue,
                spreadsheetId,
                $"{MetadataSheetName}!{MessageIdColumn.Letter}{rowNumber}"
            );
            request.ValueInputOption = UpdateRequest.ValueInputOptionEnum.RAW;

            await request.ExecuteAsync();
        }

        public async Task RemoveEventAsync(int eventKey)
        {
            var rowNumber = await GetEventRowNumber(eventKey);
            var responseSheetId = await GetSheetIdFromTitleAsync(eventKey.ToString());

            var requestParameters = new BatchUpdateSpreadsheetRequest()
            {
                Requests = new[]
                {
                    SheetsServiceRequestsHelper.RemoveEventMetadata(metadataSheetId, rowNumber),
                    SheetsServiceRequestsHelper.RemoveEventResponses(responseSheetId)
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

        public async Task<DiscordEvent> GetEventFromMessageIdAsync(ulong messageId)
        {
            var discordEvents = await ListEventsAsync();

            var result = discordEvents.FirstOrDefault(discordEvent => discordEvent.MessageId == messageId);
            if (result == null)
            {
                throw new EventNotFoundException($"Message ID {messageId} not recognised");
            }

            return result;
        }

        public async Task<IEnumerable<DiscordEvent>> ListEventsAsync()
        {
            try
            {
                var request = sheetsService.Spreadsheets.Values.Get(
                    spreadsheetId,
                    $"{MetadataSheetName}!{KeyColumn.Letter}:{MessageIdColumn.Letter}"
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
                        (DateTime) row[TimeColumn.Index],
                        (string) row[TimeZoneColumn.Index],
                        ParseMessageId(row)
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
        public async Task AddResponseForUserAsync(int eventKey, ulong userId, string responseEmoji)
        {
        }

        public async Task ClearResponsesForUserAsync(int eventKey, ulong userId)
        {
        }

        public async Task<Dictionary<ulong, IEnumerable<EventResponse>>> GetSignupsByUserAsync(int eventId)
        {
            return new Dictionary<ulong, IEnumerable<EventResponse>>
            {
                { 0UL, new[] { new EventResponse(":white_check_mark:", "Yes") } },
                { 1UL, new[] { new EventResponse(":grey_question:", "Maybe") } }
            };
        }

        public async Task<Dictionary<EventResponse, IEnumerable<ulong>>> GetSignupsByResponseAsync(int eventId)
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

        private int GetSheetIdFromTitle(string title)
        {
            var spreadsheet = sheetsService.Spreadsheets.Get(spreadsheetId).Execute();
            var sheets = spreadsheet.Sheets;

            return FindSheetId(sheets, title);
        }

        private async Task<int> GetSheetIdFromTitleAsync(string title)
        {
            var spreadsheet = await sheetsService.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
            var sheets = spreadsheet.Sheets;

            return FindSheetId(sheets, title);
        }

        private int FindSheetId(IEnumerable<Sheet> sheets, string title)
        {
            var metadataSheet = sheets.FirstOrDefault(sheet => sheet.Properties.Title == title);

            if (metadataSheet?.Properties.SheetId == null)
            {
                throw new SheetNotFoundException();
            }

            return metadataSheet.Properties.SheetId.Value;
        }

        private int GetMetadataSheetId()
        {
            try
            {
                return GetSheetIdFromTitle(MetadataSheetName);
            }
            catch (SheetNotFoundException)
            {
                throw new EventsSheetsInitialisationException("Could not find metadata sheet");
            }
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

        private ulong? ParseMessageId(IList<object> row)
        {
            // The messageId might have been trimmed off the end of the row because it was empty
            // so check for this.
            var rowContainsMessageId = row.Count < MessageIdColumn.Index + 1;
            if (rowContainsMessageId)
            {
                return null;
            }

            var cellContents = (string) row[MessageIdColumn.Index];

            var messageIdCellIsEmpty = cellContents == "";
            if (messageIdCellIsEmpty)
            {
                return null;
            }

            return ulong.Parse(cellContents);
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
