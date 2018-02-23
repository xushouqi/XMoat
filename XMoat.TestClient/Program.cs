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

            for (int i = 0; i < 3; i++)
            {
                TryConnectKService();
            }

            Console.ReadKey();
        }

        private static async void TryConnectTService()
        {
            var xService = new TService(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0));
            var channel = await xService.ConnectChannelAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1234));
            if (channel != null)
            {
                Log.Info($"TryConnectTService Success: channelId={channel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}, ipEndPoint={((TChannel)channel).RemoteAddress}");

                for (int i = 0; i < 4; i++)
                {
                    var words = $"data={i}";
                    var data = System.Text.Encoding.UTF8.GetBytes(words);
                    channel.Send(data);
                    Log.Info($"ConnectChannelAsync.Send: channelId={channel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}, {words}");
                }
            }
            else
                Log.Error("TryConnectTService Error!!!");
        }
        private static async void TryConnectKService()
        {
            var xService = new KService(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0));
            var channel = await xService.ConnectChannelAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1234));
            if (channel != null)
            {  
                Log.Info($"TryConnectKService Success: channelId={channel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}, ipEndPoint={((KChannel)channel).ClientSocket.Client.LocalEndPoint.ToString()}");

                for (int i = 0; i < 5; i++)
                {
                    var words = $"data={i}";
                    var data = System.Text.Encoding.UTF8.GetBytes(words);
                    channel.Send(data);
                    Log.Info($"ConnectChannelAsync.Send: channelId={channel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}, {words}");
                }
            }
            else
                Log.Error("TryConnectKService Error!!!");
        }
    }
}
