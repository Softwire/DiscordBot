using System;

namespace DiscordBot.DataAccess.Exceptions
{
    public class EventsSheetsInitialisationException : EventsSheetsException
    {
        public EventsSheetsInitialisationException(string? message) : base(message) { }

        public EventsSheetsInitialisationException(string? message, Exception? innerException)
            : base(message, innerException) { }
    }
}
