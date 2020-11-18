using System;

namespace DiscordBot.DataAccess.Exceptions
{
    class SheetNotFoundException : EventsSheetsException
    {
        public SheetNotFoundException(string? message) : base(message)
        {
        }
    }
}
