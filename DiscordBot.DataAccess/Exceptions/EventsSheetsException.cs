using System;

namespace DiscordBot.DataAccess.Exceptions
{
    public class EventsSheetsException : Exception
    {
        public EventsSheetsException(string? message)
            : base(message)
        {
        }

        public EventsSheetsException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
