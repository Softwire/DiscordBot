using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
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

        [Command("event")]
        public async Task Event(CommandContext context)
        {
            await context.RespondAsync(
                $"{context.Member.Mention} - choose one of the actions below or answer ``stop`` to cancel. (time out in 30s)\n" +
                "``create`` - create new event.\n" +
                "``rm`` - delete event.\n" +
                "``edit`` - edit event.\n"
            );

            var interactivity = context.Client.GetInteractivityModule();
            var response = await interactivity.WaitForMessageAsync(
                message =>
                    IsValidResponse(message.Content, EventOperations),
                TimeSpan.FromSeconds(30));

            if (response == null)
            {
                await context.RespondAsync($"{context.Member.Mention} - timed out");
            }
            else
            {
                await context.RespondAsync(response.Message.Content);
            }
        }

        private bool IsValidResponse(string response, IEnumerable<string> validResponses)
        {
            return validResponses.Contains(response);
        }
    }
}