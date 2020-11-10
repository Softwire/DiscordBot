using System.Threading.Tasks;
using DiscordBot.Commands;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;

namespace DiscordBot
{
    internal class Program
    {
        private static CommandsNextModule commands;
        private static InteractivityModule interactivity;

        private static void Main()
        {
            MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            var discord = new DiscordClient(new DiscordConfiguration
            {
                Token = secrets.RELEASE_BOT_TOKEN,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = true,
                LogLevel = LogLevel.Debug
            });

            discord.MessageCreated += async e =>
            {
                if (e.Message.Content.ToLower().StartsWith("ping"))
                    await e.Message.RespondAsync("pong!");
            };

            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefix = "?"
            });

            commands.RegisterCommands<EventCommands>();

            interactivity = discord.UseInteractivity(new InteractivityConfiguration());

            await discord.ConnectAsync();
            await Task.Delay(-1);
        }
    }
}
