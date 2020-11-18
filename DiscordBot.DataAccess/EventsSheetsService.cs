using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.DataAccess.Exceptions;
using Google;
using DiscordBot.DataAccess.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using static Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource;

namespace DiscordBot.DataAccess
{
    public interface IEventsSheetsService
    {
        Task AddEventAsync(
            string name,
            string description,
            DateTime time,
            IEnumerable<EventResponse>? responses = null
        );
        Task EditEventAsync(
            int eventKey,
            string? description = null,
            string? name = null,
            DateTime? time = null
        );
        Task AddMessageIdToEventAsync(int eventKey, ulong messageId);
        Task RemoveEventAsync(int eventKey);

        Task<DiscordEvent> GetEventAsync(int eventKey);
        Task<DiscordEvent> GetEventFromMessageIdAsync(ulong messageId);
        Task<IEnumerable<DiscordEvent>> ListEventsAsync();

        Task AddResponseForUserAsync(int eventKey, ulong userId, string responseEmoji);
        Task ClearResponsesForUserAsync(int eventKey, ulong userId);

        Task<Dictionary<EventResponse, IEnumerable<ulong>>> GetSignupsByResponseAsync(int eventId);
    }

    public class EventsSheetsService : UnsafeEventsSheetsService
    {
        private readonly SemaphoreSlim sheetsSemaphore;

        public EventsSheetsService()
        {
            sheetsSemaphore = new SemaphoreSlim(1);
        }

        public override async Task AddEventAsync(
            string name,
            string description,
            DateTime time,
            IEnumerable<EventResponse>? responses = null
        )
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await base.AddEventAsync(name, description, time, responses);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public override async Task EditEventAsync(
            int eventKey,
            string? description = null,
            string? name = null,
            DateTime? time = null
        )
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await base.EditEventAsync(eventKey, description, name, time);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public override async Task AddMessageIdToEventAsync(int eventKey, ulong messageId)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await base.AddMessageIdToEventAsync(eventKey, messageId);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public override async Task RemoveEventAsync(int eventKey)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await base.RemoveEventAsync(eventKey);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public override async Task<DiscordEvent> GetEventAsync(int eventKey)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                return await base.GetEventAsync(eventKey);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public override async Task<DiscordEvent> GetEventFromMessageIdAsync(ulong messageId)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                return await base.GetEventFromMessageIdAsync(messageId);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public override async Task<IEnumerable<DiscordEvent>> ListEventsAsync()
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                return await base.ListEventsAsync();
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public override async Task AddResponseForUserAsync(int eventKey, ulong userId, string responseEmoji)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await base.AddResponseForUserAsync(eventKey, userId, responseEmoji);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public override async Task ClearResponsesForUserAsync(int eventKey, ulong userId)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await base.ClearResponsesForUserAsync(eventKey, userId);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public override async Task<Dictionary<EventResponse, IEnumerable<ulong>>> GetSignupsByResponseAsync(int eventId)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                return await base.GetSignupsByResponseAsync(eventId);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }
    }
}
