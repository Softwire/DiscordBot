using System;
using System.Collections.Generic;
using System.Linq;
using DiscordBot.DataAccess.Models;
using Google.Apis.Sheets.v4.Data;

namespace DiscordBot.DataAccess
{
    internal static class SheetsServiceRequestsHelper
    {
        internal static Request AddEventMetadata(
            int metadataSheetId,
            int eventKey,
            string name,
            string description,
            DateTime time
        )
        {
            var appendCellsRequest = new AppendCellsRequest()
            {
                Rows = new[]
                {
                    new RowData()
                    {
                        Values = new List<CellData>()
                        {
                            MakeCellData(eventKey),
                            MakeCellData(name),
                            MakeCellData(description),
                            MakeCellData(time.ToString("s")),
                            MakeCellData("Europe/London")
                        }
                    }
                },
                SheetId = metadataSheetId,
                Fields = "*"
            };

            return new Request() { AppendCells = appendCellsRequest };
        }

        internal static Request AddResponseSheet(int eventKey)
        {
            var addResponseSheet = new AddSheetRequest()
            {
                Properties = new SheetProperties()
                {
                    Title = eventKey.ToString(),
                    SheetId = eventKey
                }
            };

            return new Request() { AddSheet = addResponseSheet };
        }

        internal static Request AddResponseColumns(int eventKey, IEnumerable<EventResponse> responses)
        {
            var responseList = responses.ToList();

            var addResponseColumns = new AppendCellsRequest()
            {
                Rows = new[]
                {
                    new RowData()
                    {
                        Values = responseList
                            .Select(response => response.Emoji)
                            .Prepend("User id / Response emoji")
                            .Select(MakeCellData)
                            .ToList()
                    },
                    new RowData()
                    {
                        Values = responseList
                            .Select(response => response.ResponseName)
                            .Prepend("Response name:")
                            .Select(MakeCellData)
                            .ToList()
                    }
                },
                SheetId = eventKey,
                Fields = "*"
            };

            return new Request() { AppendCells = addResponseColumns };
        }

        internal static Request RemoveRow(int sheetId, int rowNumber)
        {
            return new Request()
            {
                DeleteDimension = new DeleteDimensionRequest()
                {
                    Range = new DimensionRange()
                    {
                        SheetId = sheetId,
                        Dimension = "ROWS",
                        StartIndex = rowNumber - 1,
                        EndIndex = rowNumber
                    }
                }
            };
        }

        internal static Request RemoveEventResponses(int responseSheetId)
        {
            return new Request()
            {
                DeleteSheet = new DeleteSheetRequest()
                {
                    SheetId = responseSheetId
                }
            };
        }

        internal static ValueRange MakeCellUpdate(string range, object value)
        {
            return new ValueRange()
            {
                Range = range,
                Values = new IList<object>[] { new[] { value } }
            };
        }

        private static CellData MakeCellData(double number)
        {
            return new CellData()
            {
                UserEnteredValue = new ExtendedValue() { NumberValue = number }
            };
        }

        private static CellData MakeCellData(string stringValue)
        {
            return new CellData()
            {
                UserEnteredValue = new ExtendedValue() { StringValue = stringValue }
            };
        }
    }
}
