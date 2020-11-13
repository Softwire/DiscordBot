using System;
using System.Linq;
using System.Threading.Tasks;
using DiscordBot.DataAccess;
using DiscordBot.DataAccess.Models;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;

namespace DiscordBot.Commands
{
    internal class EventCommands
    {
        private static readonly string[] EventOperations =
        {
            "create",
            "rm",
            "edit",
            "stop"
        };
        private static readonly string[] EventFields =
        {
            "name",
            "description",
            "time",
            "save",
            "stop"
        };

        [Command("event")]
        [Description("Initiates the wizard for event-related actions")]
        public async Task Event(CommandContext context)
        {
            var interactivity = context.Client.GetInteractivityModule();

            await context.RespondAsync(
                $"{context.Member.Mention} - choose one of the actions below or answer ``stop`` to cancel. (time out in 30s)\n" +
                "``create`` - create new event.\n" +
                "``rm`` - delete event.\n" +
                "``edit`` - edit event.\n"
            );

            var eventOperation = await GetUserResponse(context, interactivity, EventOperations);
            if (eventOperation == null)
            {
                return;
            }

            switch (eventOperation)
            {
                case "create":
                    await CreateEvent(context, interactivity);
                    break;
                case "rm":
                    await context.RespondAsync("Not implemented yet!");
                    break;
                case "edit":
                    await EditEvent(context, interactivity);
                    break;
            }
        }

        public async Task CreateEvent(CommandContext context, InteractivityModule interactivity)
        {
            await context.RespondAsync($"{context.Member.Mention} - what is the name of the event?");
            var eventName = await GetUserResponse(context, interactivity);
            if (eventName == null)
            {
                return;
            }
            
            await context.RespondAsync($"{context.Member.Mention} - give an event description.");
            var eventDescription = await GetUserResponse(context, interactivity);
            if (eventDescription == null)
            {
                return;
            }

            await context.RespondAsync($"{context.Member.Mention} - what time is your event?");
            var eventTime = await GetUserTimeResponse(context, interactivity);
            if (eventTime == null)
            {
                return;
            }

            var eventsSheetService = context.Dependencies.GetDependency<IEventsSheetsService>();

            await eventsSheetService.AddEventAsync(eventName, eventDescription, eventTime.Value.DateTime);

            var discordEmbed = new DiscordEmbedBuilder
            {
                Title = eventName,
                Description = eventDescription,
                Color = new DiscordColor(0xFFFFFF),
                Timestamp = eventTime
            };

            await context.RespondAsync(embed: discordEmbed);
        }

        public async Task EditEvent(CommandContext context, InteractivityModule interactivity)
        {
            await context.RespondAsync($"{context.Member.Mention} - what is the event key?");
            var eventKey = await GetUserIntResponse(context, interactivity);
            if (eventKey == null)
            {
                return;
            }

            var discordEmbed = await GetEventEmbed(context, eventKey.Value);
            if (discordEmbed == null)
            {
                return;
            }

            await EditEventFields(context, interactivity, eventKey.Value, discordEmbed);
        }

        private static async Task EditEventFields(
            CommandContext context,
            InteractivityModule interactivity,
            int eventKey,
            DiscordEmbedBuilder discordEmbed)
        {
            string? newName = null;
            string? newDescription = null;
            DateTimeOffset? newTime = null;

            while (true)
            {
                await context.RespondAsync($"{context.Member.Mention}", embed: discordEmbed);
                await context.RespondAsync(
                    $"{context.Member.Mention} - what field do you want to edit? (``name``, ``description``, ``time``)\n" +
                    "``save`` to save changes.");
                var editField = await GetUserResponse(context, interactivity, EventFields);
                if (editField == null)
                {
                    return;
                }

                switch (editField)
                {
                    case "name":
                        await context.RespondAsync($"{context.Member.Mention} - enter the new event name.");
                        newName = await GetUserResponse(context, interactivity);
                        if (newName == null)
                        {
                            return;
                        }

                        discordEmbed.Title = newName;
                        break;
                    case "description":
                        await context.RespondAsync($"{context.Member.Mention} - enter the new description.");
                        newDescription = await GetUserResponse(context, interactivity);
                        if (newDescription == null)
                        {
                            return;
                        }

                        discordEmbed.Description = newDescription;
                        break;
                    case "time":
                        await context.RespondAsync($"{context.Member.Mention} - enter the new event time.");
                        newTime = await GetUserTimeResponse(context, interactivity);
                        if (newTime == null)
                        {
                            return;
                        }

                        discordEmbed.Timestamp = newTime.Value.DateTime;
                        break;
                    case "save":
                        try
                        {
                            var eventsSheetService = context.Dependencies.GetDependency<IEventsSheetsService>();
                            await eventsSheetService.EditEventAsync(eventKey, newDescription, newName, newTime?.DateTime);
                        }
                        catch (EventNotFoundException)
                        {
                            await context.RespondAsync($"{context.Member.Mention} - operation stopped: event not found");
                        }

                        await context.RespondAsync($"{context.Member.Mention} - changes saved!");
                        return;
                }
            }
        }

        private static async Task<DiscordEmbedBuilder?> GetEventEmbed(CommandContext context, int eventKey)
        {
            var eventsSheetService = context.Dependencies.GetDependency<IEventsSheetsService>();
            
            try
            {
                var discordEvent = await eventsSheetService.GetEventAsync(eventKey);

                return new DiscordEmbedBuilder
                {
                    Title = discordEvent.Name,
                    Description = discordEvent.Description,
                    Timestamp = new DateTimeOffset(discordEvent.Time)
                };
            }
            catch (EventNotFoundException)
            {
                await context.RespondAsync($"{context.Member.Mention} - operation stopped: event doesn't exist.");
                return null;
            }
        }

        private static async Task<string?> GetUserResponse(
            CommandContext context,
            InteractivityModule interactivity,
            string[]? validStrings = null)
        {
            var response = interactivity.WaitForMessageAsync(
                message =>
                    IsValidResponse(message, context, validStrings),
                TimeSpan.FromSeconds(30)
            ).Result?.Message.Content;

            switch (response)
            {
                case "stop":
                    await context.RespondAsync($"{context.User.Mention} - operation stopped.");
                    return null;
                case null:
                    await context.RespondAsync($"{context.User.Mention} - timed out.");
                    break;
            }

            return response;
        }

        private static async Task<DateTimeOffset?> GetUserTimeResponse(
            CommandContext context,
            InteractivityModule interactivity)
        {
            var response = await GetUserResponse(context, interactivity);

            if (response == null)
            {
                return null;
            }

            try
            {
                return DateTimeOffset.Parse(response);
            }
            catch (FormatException exception)
            {
                await context.RespondAsync($"{context.Member.Mention} - operation stopped: {exception.Message}");
                return null;
            }
        }

        private static async Task<int?> GetUserIntResponse(
            CommandContext context,
            InteractivityModule interactivity)
        {
            var response = await GetUserResponse(context, interactivity);

            if (response == null)
            {
                return null;
            }

            try
            {
                return int.Parse(response);
            }
            catch (FormatException exception)
            {
                await context.RespondAsync($"{context.Member.Mention} - operation stopped: {exception.Message}");
                return null;
            }
        }

        private static bool IsValidResponse(DiscordMessage response, CommandContext context, string[]? validStrings)
        {
            return context.User.Id == response.Author.Id &&
                   context.Channel == response.Channel &&
                   (validStrings == null || validStrings.Contains(response.Content));
        }
    }
}