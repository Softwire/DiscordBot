using System;
using System.Linq;
using System.Threading.Tasks;
using DiscordBot.DataAccess;
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
            "list",
            "remove",
            "show",
            "edit",
            "stop"
        };
        private static readonly string[] EventFields =
        {
            "name",
            "description",
            "time",
            "stop"
        };
        private static readonly string[] ConfirmationResponses =
        {
            "yes",
            "no"
        };

        [Command("event")]
        [Description("Initiates the wizard for event-related actions")]
        public async Task Event(CommandContext context)
        {
            var interactivity = context.Client.GetInteractivityModule();

            await context.RespondAsync(
                $"{context.Member.Mention} - choose one of the actions below or answer ``stop`` to cancel. (time out in 30s)\n" +
                "``create`` - create new event.\n" +
                "``list`` - list events\n" +
                "``remove`` - delete event.\n" +
                "``show`` - show event details.\n" +
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
                case "list":
                    await ListEvents(context, interactivity);
                    break;
                case "remove":
                    await RemoveEvent(context, interactivity);
                    break;
                case "show":
                    await ShowEvent(context, interactivity);
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

            var eventEmbed = new DiscordEmbedBuilder
            {
                Title = eventName,
                Description = eventDescription,
                Color = new DiscordColor(0xFFFFFF),
                Timestamp = eventTime
            };

            await context.RespondAsync(embed: eventEmbed);
        }

        public async Task RemoveEvent(CommandContext context, InteractivityModule interactivity)
        {
            await context.RespondAsync($"{context.Member.Mention} - which event do you want to delete");
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

            await context.RespondAsync(
                $"{context.Member.Mention} - is this the event you want to delete? (``yes``/``no``)",
                embed: discordEmbed);
            var confirmationResponse = await GetUserConfirmation(context, interactivity);
            if (confirmationResponse == null)
            {
                return;
            }

            if (confirmationResponse.Value)
            {
                try
                {
                    await context.Dependencies.GetDependency<IEventsSheetsService>()
                        .RemoveEventAsync(eventKey.Value);
                    await context.RespondAsync($"{context.Member.Mention} - poof! It's gone.");
                }
                catch (EventNotFoundException)
                {
                    await context.RespondAsync($"{context.Member.Mention} - operation stopped: event not found.");
                }
            }
        }

        public async Task ShowEvent(CommandContext context, InteractivityModule interactivity)
        {
            await context.RespondAsync($"{context.Member.Mention} - what is the event key?");
            var eventKey = await GetUserIntResponse(context, interactivity);
            if (eventKey == null)
            {
                return;
            }
            
            var eventEmbed = await GetEventEmbed(context, eventKey.Value);
            if (eventEmbed == null)
            {
                return;
            }
            
            await context.RespondAsync($"{context.Member.Mention} - here is the event", embed: eventEmbed);
  
        public async Task ListEvents(CommandContext context, InteractivityModule interactivity)
        {
            var eventsSheetsService = context.Dependencies.GetDependency<IEventsSheetsService>();
            var eventsList = await eventsSheetsService.ListEventsAsync();

            var eventsListEmbed = new DiscordEmbedBuilder
            {
                Title = "Events"
            };

            foreach (var discordEvent in eventsList)
            {
                eventsListEmbed.AddField(
                    $"{discordEvent.Key}) {discordEvent.Name}",
                    $"{discordEvent.Time}"
                );
            }

            await context.RespondAsync($"{context.Member.Mention} - here are all created events.", embed: eventsListEmbed);
        }

        public async Task EditEvent(CommandContext context, InteractivityModule interactivity)
        {
            await context.RespondAsync($"{context.Member.Mention} - what is the event key?");
            var eventKey = await GetUserIntResponse(context, interactivity);
            if (eventKey == null)
            {
                return;
            }

            var eventEmbed = await GetEventEmbed(context, eventKey.Value);
            if (eventEmbed == null)
            {
                return;
            }

            await context.RespondAsync($"{context.Member.Mention}", embed: eventEmbed);
            await context.RespondAsync(
                $"{context.Member.Mention} - what field do you want to edit? (``name``, ``description``, ``time``)\n");
            var editField = await GetUserResponse(context, interactivity, EventFields);
            if (editField == null)
            {
                return;
            }

            await EditEventField(context, interactivity, eventKey.Value, editField, eventEmbed);
        }

        private static async Task EditEventField(
            CommandContext context,
            InteractivityModule interactivity,
            int eventKey,
            string editField,
            DiscordEmbedBuilder eventEmbed)
        {
            switch (editField)
            {
                case "name":
                    await context.RespondAsync($"{context.Member.Mention} - enter the new event name.");
                    var newName = await GetUserResponse(context, interactivity);
                    if (newName == null)
                    {
                        return;
                    }

                    eventEmbed.Title = newName;
                    await TryEditEvent(context, eventKey, eventEmbed, newName: newName);
                    break;
                case "description":
                    await context.RespondAsync($"{context.Member.Mention} - enter the new description.");
                    var newDescription = await GetUserResponse(context, interactivity);
                    if (newDescription == null)
                    {
                        return;
                    }

                    eventEmbed.Description = newDescription;
                    await TryEditEvent(context, eventKey, eventEmbed, newDescription: newDescription);
                    break;
                case "time":
                    await context.RespondAsync($"{context.Member.Mention} - enter the new event time.");
                    var newTime = await GetUserTimeResponse(context, interactivity);
                    if (newTime == null)
                    {
                        return;
                    }

                    eventEmbed.Timestamp = newTime.Value.DateTime;
                    await TryEditEvent(context, eventKey, eventEmbed, newTime: newTime.Value.DateTime);
                    break;
            }
        }

        private static async Task TryEditEvent(
            CommandContext context,
            int eventKey,
            DiscordEmbedBuilder eventEmbed,
            string? newName = null,
            string? newDescription = null,
            DateTime? newTime = null)
        {
            try
            {
                var eventsSheetService = context.Dependencies.GetDependency<IEventsSheetsService>();
                await eventsSheetService.EditEventAsync(eventKey, newDescription, newName, newTime);
                await context.RespondAsync($"{context.Member.Mention} - changes saved!", embed: eventEmbed);
            }
            catch (EventNotFoundException)
            {
                await context.RespondAsync($"{context.Member.Mention} - operation stopped: event not found");
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

        private static async Task<bool?> GetUserConfirmation(
            CommandContext context,
            InteractivityModule interactivity)
        {
            var response = interactivity.WaitForMessageAsync(
                message =>
                    IsValidResponse(message, context, ConfirmationResponses),
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

            return response == "yes";
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