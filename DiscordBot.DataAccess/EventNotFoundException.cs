using System;

namespace DiscordBot.DataAccess
{
    public class EventNotFoundException : Exception
    {
        public EventNotFoundException(string? message) : base(message) { }
    }
}
