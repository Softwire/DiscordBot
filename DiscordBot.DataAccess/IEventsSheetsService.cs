using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DiscordBot.Models;

namespace DiscordBot.DataAccess
{
    interface IEventsSheetsService
    {
        Task AddEventAsync(string name, string description, DateTime time, Dictionary<string, string> roleEmotes);
        Task EditEventAsync(int key, string? description, string? name, DateTime? time);
        Task RemoveEventAsync(int key);

        Task<DiscordEvent> GetEvent(int eventKey);
        Task<IEnumerable<DiscordEvent>> ListEventsAsync();

        Task<string> GetRoleOfEmote(int eventKey, string emote);

        Task EditSignUp(int eventKey, string userId, string role, bool roleStatus);
        Task<Dictionary<string, Dictionary<string, bool>>> GetSignUps(int key);
    }
}
