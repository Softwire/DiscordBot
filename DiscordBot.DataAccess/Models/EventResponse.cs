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

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EventResponse) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Emoji, ResponseName);
        }
    }
}
