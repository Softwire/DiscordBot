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
    internal class UnsafeEventsSheetsService : IEventsSheetsService
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

        public UnsafeEventsSheetsService()
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

            var request = sheetsService.Spreadsheets.BatchUpdate(requests, spreadsheetId);
            await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);
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
                    $"{MetadataSheetName}!{DescriptionColumn.Letter}{rowNumber}",
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
            await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);
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

            await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);
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
            await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);
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
                var response = await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);

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

        public async Task AddResponseBatchAsync(IEnumerable<ResponseReaction> reactions)
        {
            // Get the sheets to find their IDs when updating
            var sheets = await GetSheets();

            // Rearrange the list of reactions into a dictionary where:
            // reactionDictionary[eventKey][userId] = list of that user's reactions to that event
            var eventList = await ListEventsAsync();
            var reactionDictionary = CreateReactionDictionary(eventList, reactions);
            var eventKeyList = reactionDictionary.Keys.ToList();

            // If there are no reactions to recognised events, there is no work to be done
            if (!eventKeyList.Any())
            {
                return;
            }

            // Make the requests to find which rows users' already have response information on,
            // and the set of response options for each event.
            // Each event sheet has two requests in the batch, for these two bits of information, grouped by event key
            var responseSheetsRequest = sheetsService.Spreadsheets.Values.BatchGet(spreadsheetId);
            responseSheetsRequest.Ranges = eventKeyList.SelectMany(eventKey =>
                new[]
                {
                    $"{eventKey}!{UserIdColumn.Letter}:{UserIdColumn.Letter}",
                    $"{eventKey}!1:2"
                }
            ).ToList();
            responseSheetsRequest.ValueRenderOption = BatchGetRequest.ValueRenderOptionEnum.FORMATTEDVALUE;
            var responseSheetsResponse =
                await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(responseSheetsRequest);

            // Make the update requests, zipping in the pair of relevant responses for each event's response sheet
            var updateRequests = eventKeyList
                .Zip(
                    responseSheetsResponse.ValueRanges.Where((userIdColumn, i) => i % 2 == 0),
                    (eventKey, userIdColumn) => new { eventKey, userIdColumn }
                )
                .Zip(
                    responseSheetsResponse.ValueRanges.Where((userIdColumn, i) => i % 2 == 1),
                    (pair, responsesOptions) => new { pair.eventKey, pair.userIdColumn, responsesOptions }
                )
                .SelectMany(triple =>
                {
                    var eventKey = triple.eventKey;
                    var sheetId = FindSheetId(sheets, eventKey.ToString());

                    var responsesOptions =
                        SheetsServiceParsingHelper.ParseResponseHeaders(triple.responsesOptions, eventKey).ToList();

                    var eventUpdates = reactionDictionary[eventKey];

                    return eventUpdates.SelectMany(entry => SheetsServiceRequestsHelper.AddUserResponsesRequests(
                        sheetId,
                        responsesOptions,
                        triple.userIdColumn,
                        entry.Key,           // User ID
                        entry.Value          // User's responses
                    ));
                });

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest()
            {
                Requests = updateRequests.ToList()
            };
            // If there are no updates to be made, don't send a request to Google Sheets
            if (batchUpdateRequest.Requests.Count == 0)
            {
                return;
            }

            var batchRequest = sheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId);
            await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(batchRequest);
        }

        public async Task ClearResponseBatchAsync(IEnumerable<ResponseReaction> reactions)
        {
            // Get the sheets to find their IDs when updating
            var sheets = await GetSheets();

            // Rearrange the list of reactions into a dictionary where:
            // usersToClearDictionary[eventKey] = list of users to have their responses cleared for that event
            // where that list only contains user IDs at most once (ie has distinct elements)
            var eventList = await ListEventsAsync();
            var usersToClearDictionary = CreateReactionDictionary(eventList, reactions).ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Keys.Distinct()
            );
            var eventKeyList = usersToClearDictionary.Keys.ToList();

            // If there are no reactions to recognised events, there is no work to be done
            if (!eventKeyList.Any())
            {
                return;
            }

            // Make the requests to find which rows the users' response information are on
            // Each event sheet has one request in the batch
            var responseSheetsRequest = sheetsService.Spreadsheets.Values.BatchGet(spreadsheetId);
            responseSheetsRequest.Ranges = eventKeyList.Select(eventKey =>
                $"{eventKey}!{UserIdColumn.Letter}:{UserIdColumn.Letter}"
            ).ToList();
            responseSheetsRequest.ValueRenderOption = BatchGetRequest.ValueRenderOptionEnum.FORMATTEDVALUE;
            var responseSheetsResponse =
                await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(responseSheetsRequest);

            // Make the update requests, zipping in the column of IDs for each event's response sheet
            var updateRequests = eventKeyList
                .Zip(
                    responseSheetsResponse.ValueRanges,
                    (eventKey, userIdColumn) => new { eventKey, userIdColumn }
                )
                .SelectMany(pair => SheetsServiceRequestsHelper.ClearUserResponsesRequests(
                    FindSheetId(sheets, pair.eventKey.ToString()),
                    pair.userIdColumn,
                    usersToClearDictionary[pair.eventKey]
                ));

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest()
            {
                Requests = updateRequests.ToList()
            };
            // If there are no updates to be made, don't send a request to Google Sheets
            if (batchUpdateRequest.Requests.Count == 0)
            {
                return;
            }

            var batchRequest = sheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId);
            await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(batchRequest);
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
            await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);
        }

        public async Task<Dictionary<EventResponse, IEnumerable<ulong>>> GetSignupsByResponseAsync(int eventId)
        {
            var request = sheetsService.Spreadsheets.Values.Get(
                spreadsheetId,
                $"{eventId}"
            );
            request.ValueRenderOption = GetRequest.ValueRenderOptionEnum.FORMATTEDVALUE;
            var response = await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);

            var responseColumns = SheetsServiceParsingHelper.ParseResponseHeaders(response, eventId).ToList();

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
                var response = SheetsServiceRequestsHelper.ExecuteRequestsWithRetries(request);

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

        private async Task<IEnumerable<Sheet>> GetSheets()
        {
            var sheetListRequest = sheetsService.Spreadsheets.Get(spreadsheetId);
            var spreadsheet = await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(sheetListRequest);
            return spreadsheet.Sheets;
        }

        private int GetSheetIdFromTitle(string title) =>
            GetSheetIdFromTitleAsync(title).Result;

        private async Task<int> GetSheetIdFromTitleAsync(string title) =>
            FindSheetId(await GetSheets(), title);

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
            var request = sheetsService.Spreadsheets.Values.Get(
                spreadsheetId,
                $"{sheetName}!{keyColumn.Letter}:{keyColumn.Letter}"
            );
            var response = await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);

            return SheetsServiceParsingHelper.FindRowNumberOfKey(response, keyColumn, numberOfHeaderRows, key);
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
            var request = sheetsService.Spreadsheets.Values.Get(
                spreadsheetId,
                $"{eventKey}!1:2"
            );
            request.ValueRenderOption = GetRequest.ValueRenderOptionEnum.FORMATTEDVALUE;

            var sheetsResponse = await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);

            return SheetsServiceParsingHelper.ParseResponseHeaders(sheetsResponse, eventKey);
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

            await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);
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

            await SheetsServiceRequestsHelper.ExecuteRequestsWithRetriesAsync(request);
        }

        private Dictionary<int, Dictionary<ulong, List<string>>> CreateReactionDictionary(
            IEnumerable<DiscordEvent> eventList,
            IEnumerable<ResponseReaction> reactions
        )
        {
            var reactionList = reactions.ToList();
            var messageIds = reactionList.Select(reaction => reaction.MessageId).Distinct();

            var messageIdDictionary = eventList
                .Where(discordEvent =>
                    discordEvent.MessageId != null && messageIds.Contains(discordEvent.MessageId.Value)
                )
                .ToDictionary(
                    discordEvent => discordEvent.MessageId!.Value,
                    discordEvent => discordEvent.Key
                );

            var eventKeyList = messageIdDictionary.Values.ToList();

            var reactionDictionary =
                eventKeyList.ToDictionary(
                    eventKey => eventKey,
                    _ => new Dictionary<ulong, List<string>>()
                );

            reactionList.ForEach(reaction =>
            {
                // If message ID is not known, ignore this reaction
                if (!messageIdDictionary.ContainsKey(reaction.MessageId))
                {
                    return;
                }

                var eventResponses = reactionDictionary[messageIdDictionary[reaction.MessageId]];

                if (!eventResponses.ContainsKey(reaction.UserId))
                {
                    eventResponses[reaction.UserId] = new List<string>();
                }
                eventResponses[reaction.UserId].Add(reaction.Emoji);
            });

            return reactionDictionary;
        }
    }
}
