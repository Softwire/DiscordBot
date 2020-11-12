namespace DiscordBot.DataAccess.Models
{
    public class Signup
    {
        public string Id { get; }
        public string Role { get; }

        public Signup(string id, string role)
        {
            Id = id;
            Role = role;
        }
    }
}