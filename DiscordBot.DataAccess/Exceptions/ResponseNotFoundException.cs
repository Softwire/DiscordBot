using System;

namespace DiscordBot.DataAccess.Exceptions
{
    public class ResponseNotFoundException : EventsSheetsException
    {
        public ResponseNotFoundException(string? message) : base(message)
        {
        }
    }
}
