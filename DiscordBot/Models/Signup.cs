namespace DiscordBot.Models
{
    class Signup
    {
        public string Id;
        public string Role;

        public Signup(string id, string role)
        {
            Id = id;
            Role = role;
        }
    }
}