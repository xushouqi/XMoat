using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace XMoat.Common
{
    public class TService : AService
    {
        private TcpListener acceptor;
        private readonly Dictionary<uint, TChannel> idChannels = new Dictionary<uint, TChannel>();

        /// <summary>
        /// 即可做client也可做server
        /// </summary>
        public TService(IPEndPoint ipEndPoint)
        {
            this.acceptor = new TcpListener(ipEndPoint);
            this.acceptor.Start();
        }

        public override void Dispose()
        {
            if (this.acceptor != null)
            {
                var ids = this.idChannels.Keys.ToArray();
                foreach (var id in ids)
                {
                    TChannel channel = this.idChannels[id];
                    channel.Dispose();
                }
                this.acceptor.Stop();
                this.acceptor = null;
            }
        }

        public override async Task<AChannel> AcceptChannelAsync()
        {
            if (this.acceptor == null)
            {
                throw new Exception("service construct must use host and port param");
            }
            TcpClient tcpClient = await this.acceptor.AcceptTcpClientAsync();
            TChannel channel = new TChannel(tcpClient, (IPEndPoint)tcpClient.Client.RemoteEndPoint, this);
            channel.OnConnected();
            this.idChannels[channel.Id] = channel;
            Log.Debug($"TService.AcceptChannelAsync: channelId={channel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            return channel;
        }

        public override async Task<AChannel> ConnectChannelAsync(IPEndPoint ipEndPoint)
        {
            TcpClient tcpClient = new TcpClient();
            TChannel channel = new TChannel(tcpClient, ipEndPoint, this);
            Log.Debug($"TService.ConnectChannelAsync.Start: channelId={channel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            await channel.ConnectAsync(ipEndPoint);
            this.idChannels[channel.Id] = channel;
            Log.Debug($"TService.ConnectChannelAsync.Finish: channelId={channel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            return channel;
        }

        public override AChannel GetChannel(uint id)
        {
            TChannel channel = null;
            this.idChannels.TryGetValue(id, out channel);
            return channel;
        }

        public override void RemoveChannel(uint channelId)
        {
            if (this.idChannels.TryGetValue(channelId, out TChannel channel))
            {
                if (channel != null)
                {
                    this.idChannels.Remove(channelId);
                    channel.Dispose();
                }
            }
        }

        public override void Update()
        {
        }
    }
}
