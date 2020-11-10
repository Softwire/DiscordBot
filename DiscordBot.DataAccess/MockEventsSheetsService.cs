using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DiscordBot.Models;

namespace DiscordBot.DataAccess
{
    public class MockEventsSheetsService : IEventsSheetsService
    {
        private Dictionary<int, MockDiscordEvent> events = new Dictionary<int, MockDiscordEvent>();

        public async Task AddEventAsync(
            string name,
            string description,
            DateTime time,
            Dictionary<string, string> roleOfEmotes
            )
        {
            var key = 1;
            if (events.Count != 0)
            {
                key = events.Keys.Max() + 1;
            }

            events.Add(key, new MockDiscordEvent(key, name, description, time, roleOfEmotes));
        }

        public async Task EditEventAsync(
            int key,
            string? description = null,
            string? name = null,
            DateTime? time = null
        )
        {
            var mockEvent = events[key];
            if (description != null)
            {
                mockEvent.Description = description;
            }

            if (name != null)
            {
                mockEvent.Name = name;
            }

            if (time != null)
            {
                mockEvent.Time = time.Value;
            }
        }

        public async Task RemoveEventAsync(int key)
        {
            events.Remove(key);
        }

        public async Task<DiscordEvent> GetEvent(int eventKey)
        {
            return events[eventKey].ToDiscordEvent();
        }

        public async Task<IEnumerable<DiscordEvent>> ListEventsAsync()
        {
            return events.Values.Select(mockEvent => mockEvent.ToDiscordEvent());
        }

        public async Task<string> GetRoleOfEmote(int eventKey, string emote)
        {
            return events[eventKey].RoleOfEmotes[emote];
        }

        public async Task EditSignUp(int eventKey, string userId, string role, bool roleStatus)
        {
            events[eventKey].Signups[userId][role] = roleStatus;
        }

        public async Task<Dictionary<string, Dictionary<string, bool>>> GetSignUps(int key)
        {
            return events[key].Signups;
        }
    }
}
