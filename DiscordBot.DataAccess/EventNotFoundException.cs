using System;

namespace DiscordBot.DataAccess
{
    class EventNotFoundException : Exception
    {
        public EventNotFoundException(string? message) : base(message) { }
    }
}
