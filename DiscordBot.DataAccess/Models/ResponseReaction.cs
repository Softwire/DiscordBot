namespace DiscordBot.DataAccess.Models
{
    public class ResponseReaction
    {
        public ulong MessageId { get; }
        public ulong UserId { get; }
        public string Emoji { get; }

        public ResponseReaction(ulong messageId, ulong userId, string emoji)
        {
            MessageId = messageId;
            UserId = userId;
            Emoji = emoji;
        }
    }
}
