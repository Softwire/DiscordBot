using System;

namespace DiscordBot.DataAccess
{
    internal static class SheetsEnvironmentVariables
    {
        const string sheetIdVar = "GOOGLE_SHEET_ID";
        const string clientEmailVar = "GOOGLE_CLIENT_EMAIL";
        const string projectIdVar = "GOOGLE_PROJECT_ID";
        const string privateKeyIdVar = "GOOGLE_PRIVATE_KEY_ID";
        const string privateKeyVar = "GOOGLE_PRIVATE_KEY";

        public static string? SheetId => Environment.GetEnvironmentVariable(sheetIdVar);
        public static string? ClientEmail => Environment.GetEnvironmentVariable(clientEmailVar);
        public static string? ProjectId => Environment.GetEnvironmentVariable(projectIdVar);
        public static string? PrivateKeyId => Environment.GetEnvironmentVariable(privateKeyIdVar);
        public static string? PrivateKey => Environment.GetEnvironmentVariable(privateKeyVar);
    }
}
