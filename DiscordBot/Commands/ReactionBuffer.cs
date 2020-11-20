using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.DataAccess;
using DiscordBot.DataAccess.Models;
using DSharpPlus;

namespace DiscordBot.Commands
{
    internal enum ReactionBufferState
    {
        Ready,
        Collecting
    }

    internal class ReactionBuffer
    {
        private const int bufferDelayMilliseconds = 10 * 1000;

        private readonly BlockingCollection<ResponseReaction> buffer;
        private readonly SemaphoreSlim bufferStateLock;
        private ReactionBufferState state;

        private readonly IEventsSheetsService eventsSheetsService;
        private readonly DiscordClient client;

        public ReactionBuffer(IEventsSheetsService eventsSheetsService, DiscordClient client)
        {
            buffer = new BlockingCollection<ResponseReaction>();
            bufferStateLock = new SemaphoreSlim(1);
            state = ReactionBufferState.Ready;

            this.eventsSheetsService = eventsSheetsService;
            this.client = client;
        }

        public async Task AddReaction(ResponseReaction reaction)
        {
            // Invariants: you may only read/write from the state and buffer variables if you hold
            // the bufferStateLock semaphore

            await bufferStateLock.WaitAsync();
            buffer.Add(reaction);

            // If another thread is collecting reactions to process, we don't need to
            if (state == ReactionBufferState.Collecting)
            {
                bufferStateLock.Release();
                return;
            }

            // Else we should start collecting reactions, wait some time for more to come in
            state = ReactionBufferState.Collecting;
            bufferStateLock.Release();
            await Task.Delay(bufferDelayMilliseconds);

            // Lock the list, get and empty its contents to be processed
            await bufferStateLock.WaitAsync();
            var reactions = buffer.GetConsumingEnumerable().ToList();
            state = ReactionBufferState.Ready;
            bufferStateLock.Release();

            await ProcessReactions(reactions);
        }

        private async Task ProcessReactions(IEnumerable<ResponseReaction> reactions)
        {
            var distinctReactions = reactions.Distinct().ToList();

            var addReactions = distinctReactions.Where(reaction => reaction.Emoji != EventCommands.ClearReaction);
            await eventsSheetsService.AddResponseBatchAsync(addReactions);

            var clearReactions = distinctReactions.Where(reaction => reaction.Emoji == EventCommands.ClearReaction);
            await eventsSheetsService.ClearResponseBatchAsync(clearReactions);

            // Send some DMs... TODO by Frank
        }
    }
}
