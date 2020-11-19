using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.DataAccess.Models;

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

        Task AddResponseBatchAsync(IEnumerable<ResponseReaction> reactions);
        Task ClearResponseBatchAsync(IEnumerable<ResponseReaction> reactions);

        Task<Dictionary<EventResponse, IEnumerable<ulong>>> GetSignupsByResponseAsync(int eventId);
    }

    public class EventsSheetsService : IEventsSheetsService
    {
        private readonly SemaphoreSlim sheetsSemaphore;
        private readonly UnsafeEventsSheetsService eventsSheetsService;

        public EventsSheetsService()
        {
            sheetsSemaphore = new SemaphoreSlim(1);
            eventsSheetsService = new UnsafeEventsSheetsService();
        }

        public async Task AddEventAsync(
            string name,
            string description,
            DateTime time,
            IEnumerable<EventResponse>? responses = null
        )
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await eventsSheetsService.AddEventAsync(name, description, time, responses);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public async Task EditEventAsync(
            int eventKey,
            string? description = null,
            string? name = null,
            DateTime? time = null
        )
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await eventsSheetsService.EditEventAsync(eventKey, description, name, time);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public async Task AddMessageIdToEventAsync(int eventKey, ulong messageId)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await eventsSheetsService.AddMessageIdToEventAsync(eventKey, messageId);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public async Task RemoveEventAsync(int eventKey)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await eventsSheetsService.RemoveEventAsync(eventKey);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public async Task<DiscordEvent> GetEventAsync(int eventKey)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                return await eventsSheetsService.GetEventAsync(eventKey);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public async Task<DiscordEvent> GetEventFromMessageIdAsync(ulong messageId)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                return await eventsSheetsService.GetEventFromMessageIdAsync(messageId);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public async Task<IEnumerable<DiscordEvent>> ListEventsAsync()
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                return await eventsSheetsService.ListEventsAsync();
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public async Task AddResponseBatchAsync(IEnumerable<ResponseReaction> reactions)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await eventsSheetsService.AddResponseBatchAsync(reactions);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public async Task ClearResponseBatchAsync(IEnumerable<ResponseReaction> reactions)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                await eventsSheetsService.ClearResponseBatchAsync(reactions);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }

        public async Task<Dictionary<EventResponse, IEnumerable<ulong>>> GetSignupsByResponseAsync(int eventId)
        {
            try
            {
                await sheetsSemaphore.WaitAsync();
                return await eventsSheetsService.GetSignupsByResponseAsync(eventId);
            }
            finally
            {
                sheetsSemaphore.Release();
            }
        }
    }
}
