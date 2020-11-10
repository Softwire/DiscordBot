using System;
using System.Collections.Generic;
using DiscordBot.Models;

namespace DiscordBot.DataAccess
{
    class MockDiscordEvent
    {
        public int Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime Time { get; set; }

        public Dictionary<string, Dictionary<string, bool>> Signups { get; }
        public Dictionary<string, string> RoleOfEmotes { get; }

        public MockDiscordEvent(
            int key,
            string name,
            string description,
            DateTime time,
            Dictionary<string, string> roleOfEmotes
            )
        {
            Key = key;
            Name = name;
            Description = description;
            Time = time;
            RoleOfEmotes = roleOfEmotes;

            Signups = new Dictionary<string, Dictionary<string, bool>>();
            RoleOfEmotes = new Dictionary<string, string>();
        }

        public DiscordEvent ToDiscordEvent() => new DiscordEvent(Name, Description, Key, Time);
    }
}
