using System.Threading.Tasks;

namespace DiscordBot.DataAccess
{
    class Program
    {
        private static async Task Main(string[] args)
        {
            var service = new EventsSheetsService();
            await service.StartService();

            await service.ReadColumns();

            await service.WriteRow();
        }
    }
}