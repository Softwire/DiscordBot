using System;

namespace DiscordBot.DataAccess.Exceptions
{
    class EventInitialisationException : Exception
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
