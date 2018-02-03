using System;
using System.Net;
using System.Threading.Tasks;
using XMoat.Common;

namespace XMoat.TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            xService = new XService(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345));
            //var channel = service.ConnectChannel(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1234));

            TryConnect();

            Console.ReadKey();
        }

        private static XService xService;

        private static async void TryConnect()
        {
            var channel = await xService.ConnectChannelAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1234));
            if (channel != null)
            {
                Log.Info($"ConnectChannelAsync Success: channelId={channel.Id}");

                for (int i = 0; i < 2; i++)
                {
                    var data = System.Text.Encoding.UTF8.GetBytes($"data={i}");
                    Log.Info($"ConnectChannelAsync.Send: data={data}");
                    channel.Send(data);
                }
            }
            else
                Log.Error("ConnectChannelAsync Error!!!");
        }
    }
}
