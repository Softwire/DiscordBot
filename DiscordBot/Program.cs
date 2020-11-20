using System;
using System.Threading.Tasks;
using DiscordBot.Commands;
using DiscordBot.DataAccess;
using DiscordBot.DataAccess.Exceptions;
using DiscordBot.DataAccess.Models;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
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

            discord.UseInteractivity(new InteractivityConfiguration());
            discord.MessageReactionAdded += async (client, eventArguments) =>
            {
                await OnMessageReactionAdded(client, eventArguments, eventsSheetsService);
            };

            await discord.ConnectAsync();
            await Task.Delay(-1);
        }

        private static async Task OnMessageReactionAdded(
            DiscordClient client,
            MessageReactionAddEventArgs eventArguments,
            IEventsSheetsService eventsSheetsService)
        {
            // Skip if event was triggered by the bot
            if (eventArguments.User == client.CurrentUser)
            {
                return;
            }

            var discordEvent = await GetEventFromMessageIdOrDefaultAsync(eventArguments.Message.Id, eventsSheetsService);
            if (discordEvent == null)
            {
                return;
            }

            await ProcessReaction(client, eventArguments, discordEvent, eventsSheetsService);
        }

        private static async Task ProcessReaction(
            DiscordClient client,
            MessageReactionAddEventArgs eventArguments,
            DiscordEvent discordEvent,
            IEventsSheetsService eventsSheetsService)
        {
            var dmChannel = await eventArguments.Guild
                .GetMemberAsync(eventArguments.User.Id).Result
                .CreateDmChannelAsync();

            switch (eventArguments.Emoji.GetDiscordName())
            {
                case (EventCommands.ClearReaction):
                    await UpdateSignupMessage(eventArguments, discordEvent, eventsSheetsService);
                    break;
                case (":no_entry_sign:"):
                    await eventsSheetsService.ClearResponsesForUserAsync(discordEvent.Key, eventArguments.User.Id);
                    await client.SendMessageAsync(
                        dmChannel,
                        $"You've signed off {discordEvent.Name}."
                    );
                    break;
                default:
                    await AddResponse(client, eventArguments, discordEvent, dmChannel, eventsSheetsService);
                    break;
            }

            await eventArguments.Message.DeleteReactionAsync(eventArguments.Emoji, eventArguments.User);
        }

        private static async Task AddResponse(
            DiscordClient client,
            MessageReactionAddEventArgs eventArguments,
            DiscordEvent discordEvent,
            DiscordChannel dmChannel,
            IEventsSheetsService eventsSheetsService)
        {
            try
            {
                await eventsSheetsService.AddResponseForUserAsync(
                    discordEvent.Key,
                    eventArguments.User.Id,
                    eventArguments.Emoji.GetDiscordName()
                );
            }
            catch (ResponseNotFoundException)
            {
                return;
            }

            await client.SendMessageAsync(
                dmChannel,
                $"You've responded to {discordEvent.Name} as {eventArguments.Emoji.Name}."
            );
        }

        private static async Task UpdateSignupMessage(
            MessageReactionAddEventArgs eventArguments,
            DiscordEvent discordEvent,
            IEventsSheetsService eventsSheetsService)
        {
            var signupsByResponse = await eventsSheetsService.GetSignupsByResponseAsync(discordEvent.Key);
            await eventArguments.Message.ModifyAsync(
                eventArguments.Message.Content,
                GetSignupEmbed(discordEvent, signupsByResponse).Build()
            );
        }
    }
}