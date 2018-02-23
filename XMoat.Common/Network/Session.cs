using System;
using System.Collections.Generic;
using System.Text;

namespace XMoat.Common
{
    public sealed class Session : Component
    {
        private static uint RpcId { get; set; }
        private NetworkComponent network;
        private AChannel channel;

        private readonly Dictionary<uint, Action<object>> requestCallback = new Dictionary<uint, Action<object>>();

        public Session(NetworkComponent netcom, AChannel ch)
        {
            //this.Id = IdGenerater.GenerateId();
            this.network = netcom;
            this.channel = ch;
            this.requestCallback.Clear();

            this.StartRecvAsync();
        }

        public override void Dispose()
        {
            if (this.Id == 0)
            {
                return;
            }

            long id = this.Id;

            base.Dispose();

            this.channel.Dispose();
            this.network.RemoveSession(id);
            this.requestCallback.Clear();
        }

        public async void StartRecvAsync()
        {
            while (true)
            {
                if (this.Id == 0)
                    return;

                Packet packet;
                try
                {
                    packet = await this.channel.Recv();
                    if (this.Id == 0)
                        return;
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
                    continue;
                }

                if (packet.Length < 2)
                {
                    Log.Error($"message error length < 2, ip: {this.channel.RemoteAddress}");
                    this.network.RemoveSession(this.Id);
                    return;
                }

                //ushort opcode = BitConverter.ToUInt16(packet.Bytes, 0);
                try
                {
                    var message = System.Text.Encoding.UTF8.GetString(packet.Bytes, 0, packet.Length);
                    Log.Info($"Session.Recv: channelId={this.channel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}: {message}");
                    //this.RunDecompressedBytes(opcode, packet.Bytes, 2, packet.Length);
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
                }
            }
        }

    }
}
