using System;
using System.Collections.Generic;

namespace DiscordBot.Models
{
    class DiscordEvent
    {
        public string Name { get; }
        public string Description { get; }
        public int Key { get; }
        public DateTime Time { get; }
        public IEnumerable<Signup> Signups = new List<Signup>();

        public DiscordEvent(string name, string description, int key, DateTime time)
        {
            Name = name;
            Description = description;
            Key = key;
            Time = time;
        }
    }
}