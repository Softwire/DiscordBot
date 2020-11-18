using System;

namespace DiscordBot.DataAccess.Exceptions
{
    public class EventNotFoundException : EventsSheetsException
    {
        public EventNotFoundException(string? message) : base(message) { }
    }
}
