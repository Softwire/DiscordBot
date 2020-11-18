using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DiscordBot.DataAccess.Exceptions;
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

        internal const string MetadataSheetName = "EventsMetadata";
        internal static readonly SheetsColumn KeyColumn = new SheetsColumn(0);
        internal static readonly SheetsColumn NameColumn = new SheetsColumn(1);
        internal static readonly SheetsColumn DescriptionColumn = new SheetsColumn(2);
        internal static readonly SheetsColumn TimeColumn = new SheetsColumn(3);
        internal static readonly SheetsColumn TimeZoneColumn = new SheetsColumn(4);
        internal static readonly SheetsColumn MessageIdColumn = new SheetsColumn(5);

        internal static readonly SheetsColumn UserIdColumn = new SheetsColumn(0);

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
            var rowNumber = await GetEventRowNumberAsync(eventKey);

            var data = new List<ValueRange>();

            if (name != null)
            {
                data.Add(SheetsServiceRequestsHelper.MakeCellUpdate(
                    $"{MetadataSheetName}!{NameColumn.Letter}{rowNumber}",
                    name
                ));
            }

            if (description != null)
            {
                data.Add(SheetsServiceRequestsHelper.MakeCellUpdate(
                    $"{MetadataSheetName}!{DescriptionColumn}{rowNumber}",
                    description
                ));
            }

            if (time != null)
            {
                data.Add(SheetsServiceRequestsHelper.MakeCellUpdate(
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
            var rowNumber = await GetEventRowNumberAsync(eventKey);

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
            var rowNumber = await GetEventRowNumberAsync(eventKey);
            var responseSheetId = await GetSheetIdFromTitleAsync(eventKey.ToString());

            var requestParameters = new BatchUpdateSpreadsheetRequest()
            {
                Requests = new[]
                {
                    SheetsServiceRequestsHelper.RemoveRow(metadataSheetId, rowNumber),
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
                        SheetsServiceParsingHelper.ParseMessageId(row)
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

        public async Task AddResponseForUserAsync(int eventKey, ulong userId, string responseEmoji)
        {
            var responseRowTask = GetResponseRowNumberAsync(eventKey, userId);
            var responseColumnsTask = GetEventResponseOptionsAsync(eventKey);

            await Task.WhenAll(responseRowTask, responseColumnsTask);
            var responseRow = responseRowTask.Result;
            var responseColumns = responseColumnsTask.Result;

            if (!responseColumns.Any(response => response.Emoji == responseEmoji))
            {
                throw new ResponseNotFoundException(
                    $"Response {responseEmoji} is not recognised for event {eventKey}"
                );
            }

            if (responseRow == null)
            {
                await AddResponseForNewUserAsync(eventKey, userId, responseEmoji, responseColumns);
            }
            else
            {
                await AddResponseForExistingUserAsync(eventKey, responseRow.Value, responseEmoji, responseColumns);
            }
        }

        public async Task ClearResponsesForUserAsync(int eventKey, ulong userId)
        {
            var rowNumber = await GetResponseRowNumberAsync(eventKey, userId);

            // If the user has already cleared their responses/never signed up in the first place
            if (rowNumber == null)
            {
                return;
            }

            var responseSheetId = await GetSheetIdFromTitleAsync(eventKey.ToString());

            var requestParameters = new BatchUpdateSpreadsheetRequest()
            {
                Requests = new[] { SheetsServiceRequestsHelper.RemoveRow(responseSheetId, rowNumber.Value) }
            };

            var request = sheetsService.Spreadsheets.BatchUpdate(requestParameters, spreadsheetId);
            await request.ExecuteAsync();
        }

        public async Task<Dictionary<EventResponse, IEnumerable<ulong>>> GetSignupsByResponseAsync(int eventId)
        {
            var request = sheetsService.Spreadsheets.Values.Get(
                spreadsheetId,
                $"{eventId}"
            );
            request.ValueRenderOption = GetRequest.ValueRenderOptionEnum.FORMATTEDVALUE;
            var response = await request.ExecuteAsync();

            if (response == null || response.Values.Count < 2)
            {
                throw new EventInitialisationException("Sign up sheet is empty");
            }

            var responseColumns = SheetsServiceParsingHelper.ParseResponseHeaders(response.Values).ToList();

            var result = responseColumns.ToDictionary(
                eventResponse => eventResponse,
                eventResponse => new List<ulong>()
            );

            response.Values.Skip(2)
                .SelectMany(row => SheetsServiceParsingHelper.ParseResponseRow(responseColumns, row))
                .ToList()
                .ForEach(pair => result[pair.response].Add(pair.userId));

            return result.ToDictionary(
                entry => entry.Key,
                entry => (IEnumerable<ulong>) entry.Value
            );
        }

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
                throw new SheetNotFoundException($"Could not find sheet with title {title}");
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

        private async Task<int?> GetRowNumberFromKeyAsync(
            string sheetName,
            SheetsColumn keyColumn,
            int numberOfHeaderRows,
            ulong key
        )
        {
            try
            {
                var request = sheetsService.Spreadsheets.Values.Get(
                    spreadsheetId,
                    $"{sheetName}!{keyColumn.Letter}:{keyColumn.Letter}"
                );
                var response = await request.ExecuteAsync();

                if (response == null || response.Values.Count < numberOfHeaderRows)
                {
                    throw new EventNotFoundException($"Event key {key} not recognised");
                }

                var rowNumber = response.Values
                    .Skip(numberOfHeaderRows)
                    .Select((values, index) => (values, index))
                    .First(row => ulong.Parse((string)row.values[keyColumn.Index]) == key);

                // Extract row number, plus a correction factor:
                // Correct for skipping the header
                // These lists are 0 indexed, but Sheets index from 1
                return rowNumber.index + numberOfHeaderRows + 1;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private async Task<int> GetEventRowNumberAsync(int eventKey)
        {
            var rowNumber = await GetRowNumberFromKeyAsync(MetadataSheetName, KeyColumn, 1, (ulong) eventKey);
            if (rowNumber == null)
            {
                throw new EventNotFoundException($"Event key {eventKey} not recognised");
            }

            return rowNumber.Value;
        }

        private async Task<int?> GetResponseRowNumberAsync(int eventKey, ulong userKey)
        {
            try
            {
                return await GetRowNumberFromKeyAsync(eventKey.ToString(), UserIdColumn, 2, userKey);
            }
            catch (GoogleApiException exception)
            {
                throw new EventInitialisationException(
                    $"Sign ups have not been released for event {eventKey}",
                    exception
                );
            }
        }

        private async Task<IEnumerable<EventResponse>> GetEventResponseOptionsAsync(int eventKey)
        {
            try
            {
                var request = sheetsService.Spreadsheets.Values.Get(
                    spreadsheetId,
                    $"{eventKey}!1:2"
                );
                request.ValueRenderOption = GetRequest.ValueRenderOptionEnum.FORMATTEDVALUE;

                var sheetsResponse = await request.ExecuteAsync();

                if (sheetsResponse == null || sheetsResponse.Values.Count < 2)
                {
                    throw new EventsSheetsInitialisationException($"Event sheet {eventKey} is empty");
                }

                return SheetsServiceParsingHelper.ParseResponseHeaders(sheetsResponse.Values);
            }
            catch (GoogleApiException)
            {
                throw new EventInitialisationException(
                    $"Could not find event responses for event {eventKey}. Has it been published yet?"
                );
            }
        }

        private async Task AddResponseForNewUserAsync(
            int eventKey,
            ulong userId,
            string responseEmoji,
            IEnumerable<EventResponse> responseColumns
        )
        {
            var newRow = responseColumns
                .Select(response => response.Emoji == responseEmoji ? 1 : 0)
                .Cast<object>()
                .Prepend(userId.ToString());

            var values = new ValueRange()
            {
                Values = new IList<object>[]
                {
                    newRow.ToList()
                }
            };

            var request = sheetsService.Spreadsheets.Values.Append(values, spreadsheetId, $"{eventKey}");
            request.ValueInputOption = AppendRequest.ValueInputOptionEnum.RAW;

            await request.ExecuteAsync();
        }

        private async Task AddResponseForExistingUserAsync(
            int eventKey,
            int responseRow,
            string responseEmoji,
            IEnumerable<EventResponse> responseColumns
        )
        {
            var index = responseColumns.ToList().FindIndex(responseOption => responseOption.Emoji == responseEmoji);

            // Plus 1 to correct for the title column that is skipped over and not in the response list.
            var updateColumn = new SheetsColumn(index + 1);
            var range = $"{eventKey}!{updateColumn.Letter}{responseRow}";

            var cellValue = SheetsServiceRequestsHelper.MakeCellUpdate(range, 1);
            var request = sheetsService.Spreadsheets.Values.Update(cellValue, spreadsheetId, range);
            request.ValueInputOption = UpdateRequest.ValueInputOptionEnum.RAW;

            await request.ExecuteAsync();
        }
    }
}
