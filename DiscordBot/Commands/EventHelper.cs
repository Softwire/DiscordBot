using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DiscordBot.DataAccess;
using DiscordBot.DataAccess.Exceptions;
using DiscordBot.DataAccess.Models;
using DSharpPlus.Entities;

namespace DiscordBot.Commands
{
    class EventHelper
    {
        public static async Task<DiscordEvent?> GetEventFromMessageIdOrDefaultAsync(ulong messageId, IEventsSheetsService eventsSheetsService)
        {
            try
            {
                return await eventsSheetsService.GetEventFromMessageIdAsync(messageId);
            }
            catch (EventNotFoundException)
            {
                return null;
            }
        }

        public static Dictionary<ulong, IEnumerable<EventResponse>> GetResponsesByUser(
            Dictionary<EventResponse, IEnumerable<ulong>> signupsByResponse)
        {
            var signupsByUser = new Dictionary<ulong, IEnumerable<EventResponse>>();

            var userIds = signupsByResponse.Values
                .SelectMany(responseSignups => responseSignups)
                .Distinct();

            foreach (var userId in userIds)
            {
                var userResponses = signupsByResponse
                    .Where(responseSignups => responseSignups.Value.Contains(userId))
                    .Select(responseSignups => responseSignups.Key);

                signupsByUser.Add(userId, userResponses);
            }

            return signupsByUser;
        }

        public static DiscordEmbedBuilder GetSignupEmbed(
            DiscordEvent discordEvent,
            Dictionary<EventResponse, IEnumerable<ulong>> signupsByResponse)
        {
            var signupsByUser = GetResponsesByUser(signupsByResponse);

            var signupEmbed = new DiscordEmbedBuilder
            {
                Title = $"{discordEvent.Name} - {discordEvent.Time:ddd dd MMM yyyy @ h:mm tt}",
                Description = discordEvent.Description,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"event key: {discordEvent.Key}"
                }
            };

            if (signupsByUser.Any())
            {
                var usersField = string.Join(
                    "\n",
                    signupsByUser.Select(response => $"<@{response.Key}>")
                );

                signupEmbed.AddField("Participants", usersField, true);

                var responsesList = signupsByUser.Select(
                    response => string.Join(" ", response.Value)
                );
                var responsesField = string.Join("\n", responsesList);

                signupEmbed.AddField("Response(s)", responsesField, true);
            }
            else
            {
                signupEmbed.AddField("Participants", "There are currently no signups.");
            }

            var optionsField = string.Join(
                "\n",
                signupsByResponse.Select(response => $"{response.Key.Emoji} - {response.Key.ResponseName}")
            );

            signupEmbed.AddField("Response options", optionsField);

            return signupEmbed;
        }
    }
}
