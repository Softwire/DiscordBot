using System;

namespace DiscordBot.DataAccess.Models
{
    public class EventResponse
    {
        public string Emoji { get; }
        public string ResponseName { get; }

        public EventResponse(string emoji, string responseName)
        {
            Emoji = emoji;
            ResponseName = responseName;
        }

        protected bool Equals(EventResponse other)
        {
            return Emoji == other.Emoji && ResponseName == other.ResponseName;
        }

        public override bool Equals(object? other)
        {
            if (other == null || other.GetType() != GetType()) return false;
            return Equals((EventResponse) other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Emoji, ResponseName);
        }

        public override string ToString()
        {
            return Emoji;
        }
    }
}
