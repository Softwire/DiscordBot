using System;

namespace DiscordBot.DataAccess.Exceptions
{
    public class EventNotFoundException : Exception
    {
        public EventNotFoundException(string? message) : base(message) { }
    }
}
