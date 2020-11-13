using System;

namespace DiscordBot.DataAccess.Models
{
    public class EventResponse
    {
        public string emoji { get; }
        public string responseName { get; }

        public EventResponse(string emoji, string responseName)
        {
            this.emoji = emoji;
            this.responseName = responseName;
        }

        protected bool Equals(EventResponse other)
        {
            return emoji == other.emoji && responseName == other.responseName;
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
            return HashCode.Combine(emoji, responseName);
        }
    }
}
