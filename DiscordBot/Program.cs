using System;
using System.Threading.Tasks;
using DiscordBot.Commands;
using DiscordBot.DataAccess;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;

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
                UseInternalLogHandler = true,
                LogLevel = LogLevel.Debug
            });

            using var builder = new DependencyCollectionBuilder();
            builder.AddInstance<IEventsSheetsService>(new EventsSheetsService());
            var dependencies = builder.Build();
            

            var commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefix = "?",
                Dependencies = dependencies
            });

            commands.RegisterCommands<EventCommands>();

            discord.UseInteractivity(new InteractivityConfiguration());

            await discord.ConnectAsync();
            await Task.Delay(-1);
        }
    }
}
