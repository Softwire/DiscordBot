using System;

namespace DiscordBot.DataAccess.Exceptions
{
    class EventInitialisationException : EventsSheetsException
    {
        public EventInitialisationException(string? message)
        : base(message)
        {
        }

        public EventInitialisationException(string? message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
