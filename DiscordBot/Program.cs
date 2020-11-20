using System;
using System.Threading.Tasks;
using DiscordBot.Commands;
using DiscordBot.DataAccess;
using DiscordBot.DataAccess.Exceptions;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static DiscordBot.Commands.EventHelper;

namespace DiscordBot
{
    internal class Program
    {
        private static void Main()
        {
            MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            var discord = new DiscordClient(new DiscordConfiguration
            {
                Token = Environment.GetEnvironmentVariable("RELEASE_BOT_TOKEN"),
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged
                          | DiscordIntents.GuildMembers,
                MinimumLogLevel = LogLevel.Debug
            });

            var eventsSheetsService = new EventsSheetsService();
            var services = new ServiceCollection()
                .AddSingleton<IEventsSheetsService>(eventsSheetsService)
                .BuildServiceProvider();

            var commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = new [] { "?" },
                Services = services
            });

            commands.RegisterCommands<EventCommands>();

            var reactionBuffer = new ReactionBuffer(eventsSheetsService);

            discord.UseInteractivity(new InteractivityConfiguration());
            discord.MessageReactionAdded += (client, eventArguments) =>
            {
                OnMessageReactionAdded(client, eventArguments, eventsSheetsService, reactionBuffer);
                return Task.CompletedTask;
            };

            await discord.ConnectAsync();
            await Task.Delay(-1);
        }

        private static void OnMessageReactionAdded(
            DiscordClient client,
            MessageReactionAddEventArgs eventArguments,
            IEventsSheetsService eventsSheetsService,
            ReactionBuffer reactionBuffer)
        {
            // Skip if event was triggered by the bot
            if (eventArguments.User == client.CurrentUser)
            {
                return;
            }

            // Skip if reaction is not in the signup channel
            if (eventArguments.Channel.Name != EventCommands.SignupChannelName)
            {
                return;
            }

            _ = Task.Run(async () =>
                await ProcessReaction(eventArguments, eventsSheetsService, reactionBuffer)
            );
        }

        private static async Task ProcessReaction(
            MessageReactionAddEventArgs eventArguments,
            IEventsSheetsService eventsSheetsService,
            ReactionBuffer reactionBuffer)
        {
            await eventArguments.Message.DeleteReactionAsync(eventArguments.Emoji, eventArguments.User);

            if (eventArguments.Emoji.GetDiscordName() == EventCommands.RefreshReaction)
            {
                await UpdateSignupMessage(eventArguments, eventsSheetsService);
            }
            else
            { 
                await reactionBuffer.AddReaction(eventArguments);
            }
        }

        private static async Task UpdateSignupMessage(
            MessageReactionAddEventArgs eventArguments,
            IEventsSheetsService eventsSheetsService)
        {
            var discordEvent = await GetEventFromMessageIdOrDefaultAsync(eventArguments.Message.Id, eventsSheetsService);
            if (discordEvent != null)
            {
                var signupsByResponse = await eventsSheetsService.GetSignupsByResponseAsync(discordEvent.Key);
                await eventArguments.Message.ModifyAsync(
                    eventArguments.Message.Content,
                    GetSignupEmbed(discordEvent, signupsByResponse).Build()
                );
            }
        }
    }
}
