using System.Collections.Generic;
using System.Linq;
using DiscordBot.DataAccess.Models;

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

        public static IEnumerable<EventResponse> ParseResponseHeaders(IList<IList<object>> sheetsResponse)
        {
            return sheetsResponse[0]
                .Zip(sheetsResponse[1])
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
    }
}
