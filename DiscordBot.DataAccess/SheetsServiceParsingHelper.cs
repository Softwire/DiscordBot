using System;
using System.Collections.Generic;
using System.Linq;
using DiscordBot.DataAccess.Exceptions;
using DiscordBot.DataAccess.Models;
using Google.Apis.Sheets.v4.Data;

namespace DiscordBot.DataAccess
{
    internal static class SheetsServiceParsingHelper
    {
        public static ulong? ParseMessageId(IList<object> row)
        {
            // The messageId might have been trimmed off the end of the row because it was empty
            // so check for this.
            var rowContainsMessageId = row.Count < UnsafeEventsSheetsService.MessageIdColumn.Index + 1;
            if (rowContainsMessageId)
            {
                return null;
            }

            var cellContents = (string)row[UnsafeEventsSheetsService.MessageIdColumn.Index];

            var messageIdCellIsEmpty = cellContents == "";
            if (messageIdCellIsEmpty)
            {
                return null;
            }

            return ulong.Parse(cellContents);
        }

        public static IEnumerable<EventResponse> ParseResponseHeaders(ValueRange sheetsResponse, int eventKey)
        {
            if (sheetsResponse == null || sheetsResponse.Values.Count < 2)
            {
                throw new EventsSheetsInitialisationException($"Event sheet {eventKey} is empty");
            }

            return sheetsResponse.Values[0]
                .Zip(sheetsResponse.Values[1])
                .Skip(1) // Skip titles column
                .Select<(object emoji, object name), EventResponse>(response =>
                    new EventResponse((string)response.emoji, (string)response.name)
                );
        }

        public static IEnumerable<(EventResponse response, ulong userId)> ParseResponseRow(
            IEnumerable<EventResponse> responseColumns,
            IList<object> row
        )
        {
            var userId = ulong.Parse((string)row[0]);

            return row.Skip(1) // Skip title column
                .Zip(responseColumns)
                .Where<(object cell, EventResponse response)>(pair =>
                    (string)pair.cell == "1"
                )
                .Select(pair => (pair.response, userId));
        }

        public static int? FindRowNumberOfKey(
            ValueRange response,
            SheetsColumn keyColumn,
            int numberOfHeaderRows,
            ulong key
        )
        {
            if (response == null || response.Values.Count < numberOfHeaderRows)
            {
                throw new EventNotFoundException($"Event key {key} not recognised");
            }

            try
            {
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
    }
}
