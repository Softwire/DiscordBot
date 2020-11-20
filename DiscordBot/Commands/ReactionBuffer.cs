using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.DataAccess;
using DiscordBot.DataAccess.Models;
using DSharpPlus.EventArgs;
using static DiscordBot.Commands.EventHelper;

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

        private readonly ConcurrentQueue<MessageReactionAddEventArgs> buffer;
        private readonly SemaphoreSlim bufferStateLock;
        private ReactionBufferState state;

        private readonly IEventsSheetsService eventsSheetsService;

        public ReactionBuffer(IEventsSheetsService eventsSheetsService)
        {
            buffer = new ConcurrentQueue<MessageReactionAddEventArgs>();
            bufferStateLock = new SemaphoreSlim(1);
            state = ReactionBufferState.Ready;

            this.eventsSheetsService = eventsSheetsService;
        }

        public async Task AddReaction(MessageReactionAddEventArgs eventArguments)
        {
            // Invariants: you may only read/write from the state and buffer variables if you hold
            // the bufferStateLock semaphore

            await bufferStateLock.WaitAsync();
            buffer.Enqueue(eventArguments);

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
            var reactions = buffer.ToList();
            buffer.Clear();

            state = ReactionBufferState.Ready;
            bufferStateLock.Release();

            await ProcessReactions(reactions);
        }

        private async Task ProcessReactions(IEnumerable<MessageReactionAddEventArgs> reactionArguments)
        {
            var reactionArgumentsList = reactionArguments.ToList();
            var reactions = reactionArgumentsList.Select(eventArguments =>
                new ResponseReaction(
                    eventArguments.Message.Id,
                    eventArguments.User.Id,
                    eventArguments.Emoji.GetDiscordName()
                )
            );

            var distinctReactions = reactions.Distinct().ToList();

            var addReactions = distinctReactions.Where(reaction => reaction.Emoji != EventCommands.ClearReaction);
            await eventsSheetsService.AddResponseBatchAsync(addReactions);

            var clearReactions = distinctReactions.Where(reaction => reaction.Emoji == EventCommands.ClearReaction);
            await eventsSheetsService.ClearResponseBatchAsync(clearReactions);

            // Update each message that had a reaction processed
            var messages = reactionArgumentsList
                .Select(eventArguments => eventArguments.Message)
                .Distinct();
            foreach (var message in messages)
            {
                var discordEvent = await eventsSheetsService.GetEventFromMessageIdAsync(message.Id);
                var signupsByResponse = await eventsSheetsService.GetSignupsByResponseAsync(discordEvent.Key);
                await message.ModifyAsync(
                    message.Content,
                    GetSignupEmbed(discordEvent, signupsByResponse).Build()
                );
            }
        }
    }
}
