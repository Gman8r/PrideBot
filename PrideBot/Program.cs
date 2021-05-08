using System.Threading.Tasks;

namespace PrideBot
{
    class Program
    {
        public static Task Main(string[] args)
            => Startup.RunAsync(args);
    }
}