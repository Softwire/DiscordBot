using System;

namespace DiscordBot.DataAccess.Models
{
    public class DiscordEvent
    {
        public int Key { get; }
        public string Name { get; }
        public string Description { get; }
        public DateTime Time { get; }
        public string TimeZone { get; }
        public ulong? MessageId { get; }

        public DiscordEvent(
            string name,
            string description,
            int key,
            DateTime time,
            string timeZone,
            ulong? messageId
        )
        {
            Name = name;
            Description = description;
            Key = key;
            Time = time;
            TimeZone = timeZone;
            MessageId = messageId;
        }
    }
}
