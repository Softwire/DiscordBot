using System;

namespace DiscordBot.DataAccess.Exceptions
{
    public class ResponseNotFoundException : Exception
    {
        public ResponseNotFoundException(string? message) : base(message)
        {
        }
    }
}
