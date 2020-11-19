using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DiscordBot.DataAccess;
using DiscordBot.DataAccess.Exceptions;
using DiscordBot.DataAccess.Models;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using static DiscordBot.Commands.EventHelper;

namespace DiscordBot.Commands
{
    [Group("event")]
    [RequireRoles(RoleCheckMode.All, "Bot Whisperer")]
    internal class EventCommands : BaseCommandModule
    {
        private static readonly string[] EventOperations =
        {
            "create",
            "list",
            "remove",
            "show",
            "edit",
            "start",
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

        private static readonly EventResponse[][] ResponseSets =
        {
            new[]
            {
                new EventResponse(":white_check_mark:", "Yes"),
                new EventResponse(":grey_question:", "Maybe")
            },

            new[]
            {
                new EventResponse(":white_check_mark:", "Yes"),
                new EventResponse(":money_with_wings:", "Need to buy it"),
                new EventResponse(":grey_question:", "Maybe")
            },

            new[]
            {
                new EventResponse(":one:", "Option 1"),
                new EventResponse(":two:", "Option 2"),
                new EventResponse(":three:", "Option 3"),
                new EventResponse(":four:", "Option 4")
            }
        };

        private readonly IEventsSheetsService eventsSheetsService;

        public EventCommands(IEventsSheetsService eventsSheetsService)
        {
            this.eventsSheetsService = eventsSheetsService;
        }

        [GroupCommand]
        [Description("Initiates the wizard for event-related actions")]
        public async Task Event(CommandContext context)
        {
            await context.RespondAsync(
                $"{context.Member.Mention} - choose one of the actions below or answer ``stop`` to cancel. (time out in 30s)\n" +
                "``create`` - create new event.\n" +
                "``list`` - list events\n" +
                "``remove`` - delete event.\n" +
                "``show`` - show event details.\n" +
                "``edit`` - edit event.\n" +
                "``start`` - open signups for event."
            );

            var eventOperation = await GetUserResponse(context, EventOperations);
            if (eventOperation == null)
            {
                return;
            }

            switch (eventOperation)
            {
                case "create":
                    await CreateEvent(context);
                    break;
                case "list":
                    await ListEvents(context);
                    break;
                case "remove":
                    await RemoveEvent(context);
                    break;
                case "show":
                    await ShowEvent(context);
                    break;
                case "edit":
                    await EditEvent(context);
                    break;
                case "start":
                    await CreateSignupSheet(context);
                    break;
            }
        }

        [Command("create")]
        public async Task CreateEvent(CommandContext context)
        {
            await context.RespondAsync($"{context.Member.Mention} - what is the name of the event?");
            var eventName = await GetUserResponse(context);
            if (eventName == null)
            {
                return;
            }

            await CreateEvent(context, eventName);
        }

        [Command("create")]
        public async Task CreateEvent(CommandContext context, string eventName)
        {
            await context.RespondAsync($"{context.Member.Mention} - give an event description.");
            var eventDescription = await GetUserResponse(context);
            if (eventDescription == null)
            {
                return;
            }

            await CreateEvent(context, eventName, eventDescription);
        }

        [Command("create")]
        public async Task CreateEvent(CommandContext context, string eventName, string eventDescription)
        {
            await context.RespondAsync($"{context.Member.Mention} - what time is your event?");
            var eventTime = await GetUserTimeResponse(context);
            if (eventTime == null)
            {
                return;
            }

            await CreateEvent(context, eventName, eventDescription, eventTime.Value.DateTime);
        }

        [Command("create")] 
        public async Task CreateEvent(CommandContext context, string eventName, string eventDescription, DateTime eventTime)
        {
            var responseEmbed = new DiscordEmbedBuilder
            {
                Title = "Response options"
            };


            var i = 0;
            foreach (var responseSet in ResponseSets)
            {
                var responseSetString = string.Join(
                    ", ",
                    responseSet.Select(response => $"{response.ResponseName} - {response.Emoji}")
                );
                responseEmbed.AddField($"Response set {i}", responseSetString);
                i += 1;
            }

            await context.RespondAsync(
                $"Which response set would you like to use? (type the number)", 
                embed: responseEmbed);
            var responseSetIndex = await GetUserIntResponse(context);

            await CreateEvent(context, eventName, eventDescription, eventTime, responseSetIndex);
        }

        [Command("create")]
        public async Task CreateEvent(
            CommandContext context,
            string eventName,
            string eventDescription,
            DateTime eventTime,
            int? responseSetIndex)
        {
            if (responseSetIndex == null || responseSetIndex.Value >= ResponseSets.Length)
            {
                await context.RespondAsync("Invalid response set number");
                return;
            }
            var responseSet = ResponseSets[responseSetIndex.Value];
            await eventsSheetsService.AddEventAsync(eventName, eventDescription, eventTime, responseSet);
            var eventEmbed = new DiscordEmbedBuilder
            {
                Title = $"{eventName} - {eventTime:ddd dd MMM yyyy @ h:mm tt}",
                Description = eventDescription
            };
            var responseSetString = string.Join(
                ", ",
                responseSet.Select(response => $"{response.ResponseName} - {response.Emoji}"));
            eventEmbed.AddField("Responses", responseSetString);

            await context.RespondAsync($"{context.Member.Mention} - your event has been added!", embed: eventEmbed);
        }

        [Command("remove")]
        public async Task RemoveEvent(CommandContext context)
        {
            await context.RespondAsync(
                $"{context.Member.Mention} - what is the event key? (use the ``list`` option to find out)");
            var eventKey = await GetUserIntResponse(context);
            if (eventKey == null)
            {
                return;
            }

            await RemoveEvent(context, eventKey.Value);
        }

        [Command("remove")]
        public async Task RemoveEvent(CommandContext context, int eventKey)
        {
            var discordEmbed = await GetEventEmbed(context, eventKey);
            if (discordEmbed == null)
            {
                return;
            }

            await context.RespondAsync(
                $"{context.Member.Mention} - is this the event you want to delete? (``yes``/``no``)",
                embed: discordEmbed);
            var confirmationResponse = await GetUserConfirmation(context);
            if (confirmationResponse == null || confirmationResponse == false)
            {
                return;
            }

            try
            {
                await eventsSheetsService
                    .RemoveEventAsync(eventKey);
                await context.RespondAsync($"{context.Member.Mention} - poof! It's gone.");
            }
            catch (EventNotFoundException)
            {
                await context.RespondAsync($"{context.Member.Mention} - operation stopped: event not found.");
            }
        }

        [Command("show")]
        public async Task ShowEvent(CommandContext context)
        {
            await context.RespondAsync($"{context.Member.Mention} - what is the event key? (use the ``list`` option to find out)");
            var eventKey = await GetUserIntResponse(context);
            if (eventKey == null)
            {
                return;
            }
            
            await ShowEvent(context, eventKey.Value);
        }

        [Command("show")]
        private async Task ShowEvent(CommandContext context, int eventKey)
        {
            var eventEmbed = await GetEventEmbed(context, eventKey);
            if (eventEmbed == null)
            {
                return;
            }

            await context.RespondAsync($"{context.Member.Mention} - here is the event", embed: eventEmbed);
        }

        [Command("list")]
        public async Task ListEvents(CommandContext context)
        {
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

        [Command("edit")]
        public async Task EditEvent(CommandContext context)
        {
            await context.RespondAsync($"{context.Member.Mention} - what is the event key? (use the ``list`` option to find out)");
            var eventKey = await GetUserIntResponse(context);
            if (eventKey == null)
            {
                return;
            }

            await EditEvent(context, eventKey.Value);
        }
        
        [Command("edit")]
        private async Task EditEvent(CommandContext context, int eventKey)
        {
            var eventEmbed = await GetEventEmbed(context, eventKey);
            if (eventEmbed == null)
            {
                return;
            }

            await context.RespondAsync($"{context.Member.Mention}", embed: eventEmbed);
            await context.RespondAsync(
                $"{context.Member.Mention} - what field do you want to edit? (``name``, ``description``, ``time``)\n");
            var editField = await GetUserResponse(context, EventFields);
            if (editField == null)
            {
                return;
            }

            await EditEvent(context, eventKey, editField, eventEmbed);
        }

        [Command("edit")]
        private async Task EditEvent(
            CommandContext context, 
            int eventKey, 
            string editField,
            DiscordEmbedBuilder? eventEmbed = null)
        {
            eventEmbed ??= await GetEventEmbed(context, eventKey);
            if (eventEmbed == null)
            {
                return;
            }
            await EditEventField(context, eventKey, editField, eventEmbed);
        }

        [Command("start")]
        private async Task CreateSignupSheet(CommandContext context)
        {
            await context.RespondAsync($"{context.Member.Mention} - what is the event key? (use the ``list`` option to find out)");
            var eventKey = await GetUserIntResponse(context);
            if (eventKey == null)
            {
                return;
            }

            await CreateSignupSheet(context, eventKey.Value);
        }

        [Command("start")]
        private async Task CreateSignupSheet(CommandContext context, int eventKey)
        {
            var eventEmbed = await GetEventEmbed(context, eventKey);
            if (eventEmbed == null)
            {
                return;
            }

            await context.RespondAsync(
                $"{context.Member.Mention} - start signups for this event? - (``yes``/``no``)",
                embed: eventEmbed);
            var confirmationResponse = await GetUserConfirmation(context);
            if (confirmationResponse == null || confirmationResponse == false)
            {
                return;
            }

            await SendSignupMessage(context, eventKey);
        }

        private async Task SendSignupMessage(CommandContext context, int eventKey)
        { 
            var discordEvent = await eventsSheetsService.GetEventAsync(eventKey);

            var signupsByResponse = await eventsSheetsService.GetSignupsByResponseAsync(eventKey);

            var signupEmbed = GetSignupEmbed(discordEvent, signupsByResponse);

            var signupMessage = await context.RespondAsync($"Signups are open for __**{discordEvent.Name}**__!", embed: signupEmbed);
            await eventsSheetsService.AddMessageIdToEventAsync(eventKey, signupMessage.Id);

            foreach (var response in signupsByResponse.Keys)
            {
                await signupMessage.CreateReactionAsync(DiscordEmoji.FromName(context.Client, response.Emoji));
            }

            await signupMessage.CreateReactionAsync(DiscordEmoji.FromName(context.Client, ":no_entry_sign:"));
        }

        private async Task EditEventField(
            CommandContext context,
            int eventKey,
            string editField,
            DiscordEmbedBuilder eventEmbed)
        {
            switch (editField)
            {
                case "name":
                    await context.RespondAsync($"{context.Member.Mention} - enter the new event name.");
                    var newName = await GetUserResponse(context);
                    if (newName == null)
                    {
                        return;
                    }

                    eventEmbed.Title = newName;
                    await TryEditEvent(context, eventKey, eventEmbed, newName: newName);
                    break;
                case "description":
                    await context.RespondAsync($"{context.Member.Mention} - enter the new description.");
                    var newDescription = await GetUserResponse(context);
                    if (newDescription == null)
                    {
                        return;
                    }

                    eventEmbed.Description = newDescription;
                    await TryEditEvent(context, eventKey, eventEmbed, newDescription: newDescription);
                    break;
                case "time":
                    await context.RespondAsync($"{context.Member.Mention} - enter the new event time.");
                    var newTime = await GetUserTimeResponse(context);
                    if (newTime == null)
                    {
                        return;
                    }

                    eventEmbed.Timestamp = newTime.Value.DateTime;
                    await TryEditEvent(context, eventKey, eventEmbed, newTime: newTime.Value.DateTime);
                    break;
            }
        }

        private async Task TryEditEvent(
            CommandContext context,
            int eventKey,
            DiscordEmbedBuilder eventEmbed,
            string? newName = null,
            string? newDescription = null,
            DateTime? newTime = null)
        {
            try
            {
                await eventsSheetsService.EditEventAsync(eventKey, newDescription, newName, newTime);
                await context.RespondAsync($"{context.Member.Mention} - changes saved!", embed: eventEmbed);
            }
            catch (EventNotFoundException)
            {
                await context.RespondAsync($"{context.Member.Mention} - operation stopped: event not found");
            }
        }

        private async Task<DiscordEmbedBuilder?> GetEventEmbed(CommandContext context, int eventKey)
        {
            try
            {
                var discordEvent = await eventsSheetsService.GetEventAsync(eventKey);

                return new DiscordEmbedBuilder
                {
                    Title = $"{discordEvent.Name} - {discordEvent.Time:ddd dd MMM yyyy @ h:mm tt}",
                    Description = discordEvent.Description,
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"event key: {discordEvent.Key}"
                    }
                };
            }
            catch (EventNotFoundException)
            {
                await context.RespondAsync($"{context.Member.Mention} - operation stopped: event doesn't exist.");
                return null;
            }
        }

        private async Task<string?> GetUserResponse(
            CommandContext context,
            string[]? validStrings = null)
        {
            var interactivity = context.Client.GetInteractivity();
            var response = interactivity.WaitForMessageAsync(
                message =>
                    IsValidResponse(message, context, validStrings),
                TimeSpan.FromSeconds(30)
            ).Result.Result?.Content;

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

        private async Task<bool?> GetUserConfirmation(CommandContext context)
        {
            var interactivity = context.Client.GetInteractivity();
            var response = interactivity.WaitForMessageAsync(
                message =>
                    IsValidResponse(message, context, ConfirmationResponses),
                TimeSpan.FromSeconds(30)
            ).Result.Result?.Content;

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

        private async Task<DateTimeOffset?> GetUserTimeResponse(CommandContext context)
        {
            var response = await GetUserResponse(context);

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

        private async Task<int?> GetUserIntResponse(CommandContext context)
        {
            var response = await GetUserResponse(context);

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

        private bool IsValidResponse(DiscordMessage response, CommandContext context, string[]? validStrings)
        {
            return context.User.Id == response.Author.Id &&
                   context.Channel == response.Channel &&
                   (validStrings == null || validStrings.Contains(response.Content));
        }
    }
}
