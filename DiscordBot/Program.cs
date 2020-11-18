using System;
using System.Threading.Tasks;
using DiscordBot.Commands;
using DiscordBot.DataAccess;
using DiscordBot.DataAccess.Exceptions;
using DiscordBot.DataAccess.Models;
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
        private static readonly IEventsSheetsService eventsSheetsService = new EventsSheetsService();

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
            if (eventArguments.User == client.CurrentUser)
            {
                return;
            }

            var discordEvent = await GetEventFromMessageIdOrDefaultAsync(eventArguments.Message.Id, eventsSheetsService);
            if (discordEvent == null)
            {
                return;
            }

            await ProcessReaction(client, eventArguments, discordEvent);

            await UpdateSignupSheet(eventArguments, discordEvent);
        }

        private static async Task ProcessReaction(
            DiscordClient client,
            MessageReactionAddEventArgs eventArguments,
            DiscordEvent discordEvent)
        {
            var dmChannel = await eventArguments.Guild
                .GetMemberAsync(eventArguments.User.Id).Result
                .CreateDmChannelAsync();

            if (eventArguments.Emoji.GetDiscordName() == ":no_entry_sign:")
            {
                await eventsSheetsService.ClearResponsesForUserAsync(discordEvent.Key, eventArguments.User.Id);
                await client.SendMessageAsync(dmChannel, $"You've signed off {discordEvent.Name}.");
            }
            else
            {
                try
                {
                    await eventsSheetsService.AddResponseForUserAsync(
                        discordEvent.Key,
                        eventArguments.User.Id,
                        eventArguments.Emoji.GetDiscordName());
                }
                catch (ResponseNotFoundException)
                {
                    return;
                }

                await client.SendMessageAsync(
                    dmChannel,
                    $"You've responded to {discordEvent.Name} as {eventArguments.Emoji.Name}.");
            }
        }

        private static async Task UpdateSignupSheet(
            MessageReactionAddEventArgs eventArguments,
            DiscordEvent discordEvent)
        {
            var signupsByResponse = await eventsSheetsService.GetSignupsByResponseAsync(discordEvent.Key);
            await eventArguments.Message.ModifyAsync(
                eventArguments.Message.Content,
                GetSignupEmbed(discordEvent, signupsByResponse).Build()
            );

            await eventArguments.Message.DeleteReactionAsync(eventArguments.Emoji, eventArguments.User);
        }
    }
}