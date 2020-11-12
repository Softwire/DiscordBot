using System;

namespace DiscordBot.DataAccess.Models
{
    public class DiscordEvent
    {
        public int Key { get; }
        public string Name { get; }
        public string Description { get; }
        public DateTime Time { get; }

        public DiscordEvent(string name, string description, int key, DateTime time)
        {
            Name = name;
            Description = description;
            Key = key;
            Time = time;
        }
    }
}