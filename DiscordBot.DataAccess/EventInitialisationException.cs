using System;

namespace DiscordBot.DataAccess
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
