using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;

namespace XMoat.Common
{
    public class KChannel : AChannel
    {
        private UdpClient socket;
        public UdpClient ClientSocket { get { return socket; } }
        private Kcp kcp;

        private readonly uint TimeoutSecs = 20;

        private readonly CircularBuffer recvBuffer = new CircularBuffer(8192);
        private readonly Queue<byte[]> sendBuffer = new Queue<byte[]>();

        private readonly PacketParser parser;
        private bool isConnected;
        private readonly IPEndPoint remoteEndPoint;

        private TaskCompletionSource<Packet> recvTcs;

        private uint lastRecvTime;

        private readonly byte[] cacheBytes = new byte[1400];

        //private uint RemoteId;

        private readonly ChannelType channelType;
        public ChannelType ChType { get { return channelType; } }

        private bool heartBeating = false;

        private KService GetService()
        {
            return (KService)this.service;
        }

        /// <summary>
        /// 创建channel，分为客户端请求和服务端接受两种情况
        /// </summary>
        /// <param name="service"></param>
        /// <param name="id"></param>
        /// <param name="remoteEndPoint"></param>
        /// <param name="socket"></param>
        public KChannel(ChannelType ctype, KService service, uint id, IPEndPoint remoteEndPoint, UdpClient socket) : base(service)
        {
            this.Id = id;
            //this.RemoteId = remoteid;
            this.remoteEndPoint = remoteEndPoint;
            this.socket = socket;
            this.parser = new PacketParser(this.recvBuffer);
            this.lastRecvTime = service.TimeNow;
            this.channelType = ctype;
            if (ctype == ChannelType.Connect)
            {
                this.Connect(service.TimeNow);
            }
            else if (ctype == ChannelType.Accept)
            {
                kcp = new Kcp(this.Id, this.OnKcpSend);
                kcp.SetMtu(512);
                kcp.NoDelay(1, 10, 2, 1);  //fast

                this.isConnected = true;
            }
        }

        /// <summary>
        /// 发送连接请求
        /// </summary>
        /// <param name="timeNow"></param>
        private void Connect(uint timeNow)
        {
            cacheBytes.WriteTo(0, (uint)KcpProtocalType.SYN);
            cacheBytes.WriteTo(4, this.Id);
            this.socket.Send(cacheBytes, 8, remoteEndPoint);

            // 200毫秒后再次update发送connect请求
            this.GetService().AddToNextTimeUpdate(timeNow + 200, this.Id);
        }
        /// <summary>
        /// 发送心跳包
        /// </summary>
        public void TryHeartbeat(uint timeNow)
        {
            if (!heartBeating && timeNow - this.lastRecvTime > TimeoutSecs * 500)
            {
                heartBeating = true;
                cacheBytes.WriteTo(0, (uint)KcpProtocalType.HEART);
                cacheBytes.WriteTo(4, this.Id);
                this.socket.Send(cacheBytes, 8, remoteEndPoint);
            }
        }
        /// <summary>
        /// 服务端收到心跳包后回应
        /// </summary>
        /// <param name="timeNow"></param>
        public void HeartbeatAck(uint timeNow)
        {
            this.lastRecvTime = timeNow;
            cacheBytes.WriteTo(0, (uint)KcpProtocalType.HEARTACK);
            cacheBytes.WriteTo(4, this.Id);
            this.socket.Send(cacheBytes, 8, remoteEndPoint);
        }
        /// <summary>
        /// 客户端收到回应，一次心跳结束
        /// </summary>
        /// <param name="timeNow"></param>
        public void FinishHeartbeat(uint timeNow)
        {
            if (heartBeating)
            {
                heartBeating = false;
                this.lastRecvTime = timeNow;
            }
        }

        /// <summary>
        /// 发送断连请求
        /// </summary>
        private void DisConnect()
        {
            cacheBytes.WriteTo(0, (uint)KcpProtocalType.FIN);
            cacheBytes.WriteTo(4, this.Id);
            //Log.Debug($"client disconnect: {this.Conn}");
            this.socket.Send(cacheBytes, 8, remoteEndPoint);
        }

        /// <summary>
        /// 收到连接请求，返回ACK
        /// </summary>
        /// <param name="remoteId"></param>
        public void HandleAccept(uint remoteId)
        {
            cacheBytes.WriteTo(0, (uint)KcpProtocalType.ACK);
            cacheBytes.WriteTo(4, this.Id);
            cacheBytes.WriteTo(8, remoteId);
            this.socket.Send(cacheBytes, 12, remoteEndPoint);
        }
        /// <summary>
        /// 收到ACK，连接建立
        /// </summary>
        /// <param name="channelId"></param>
        public void HandleConnnect(uint channelId)
        {
            if (this.isConnected)
                return;
            this.isConnected = true;

            //id替换为服务端分配的
            this.Id = channelId;
            this.kcp = new Kcp(channelId, this.OnKcpSend);
            kcp.SetMtu(512);
            kcp.NoDelay(1, 10, 2, 1);  //fast

            //有缓存的数据包发出去
            while (this.sendBuffer.Count > 0)
            {
                byte[] buffer = this.sendBuffer.Dequeue();
                this.KcpSend(buffer);
            }
        }
        /// <summary>
        /// 处理收到的正常数据包
        /// </summary>
        /// <param name="date"></param>
        /// <param name="timeNow"></param>
        public void HandleRecv(byte[] data, uint timeNow)
        {
            this.kcp.Input(data);
            // 加入update队列
            this.GetService().AddToUpdate(this.Id);

            while (true)
            {
                int n = kcp.PeekSize();
                if (n == 0)
                {
                    this.OnError(this, SocketError.NetworkReset);
                    return;
                }
                int count = this.kcp.Recv(cacheBytes);
                if (count <= 0)
                    return;

                // 收到的数据放入缓冲区
                this.recvBuffer.SendTo(this.cacheBytes, 0, count);

                lastRecvTime = timeNow;

                if (this.recvTcs != null)
                {
                    bool isOK = this.parser.Parse();
                    //数据包已接收完毕
                    if (isOK)
                    {
                        Packet packet = this.parser.GetPacket();

                        var tcs = this.recvTcs;
                        this.recvTcs = null;
                        tcs.SetResult(packet);
                    }
                }
            }
        }

        private void KcpSend(byte[] buffers)
        {
            this.kcp.Send(buffers);
            this.GetService().AddToUpdate(this.Id);
        }

        void OnKcpSend(byte[] data, int count)
        {
            this.socket.Send(data, count, this.remoteEndPoint);
        }

        public override void Send(byte[] buffer)
        {
            byte[] size = BitConverter.GetBytes((ushort)buffer.Length);
            if (isConnected)
            {
                this.KcpSend(size);
                this.KcpSend(buffer);
                return;
            }
            this.sendBuffer.Enqueue(size);
            this.sendBuffer.Enqueue(buffer);
        }

        public override void Send(List<byte[]> buffers)
        {
            ushort size = (ushort)buffers.Select(b => b.Length).Sum();
            byte[] sizeBuffer = BitConverter.GetBytes(size);
            if (isConnected)
            {
                this.KcpSend(sizeBuffer);
            }
            else
            {
                this.sendBuffer.Enqueue(sizeBuffer);
            }

            foreach (byte[] buffer in buffers)
            {
                if (isConnected)
                {
                    this.KcpSend(buffer);
                }
                else
                {
                    this.sendBuffer.Enqueue(buffer);
                }
            }
        }

        public override Task<Packet> Recv()
        {
            if (this.Id == 0)
            {
                throw new Exception("KChannel already Disposed!!!");
            }

            bool isOK = this.parser.Parse();
            if (isOK)
            {
                Packet packet = this.parser.GetPacket();
                return Task.FromResult(packet);
            }

            recvTcs = new TaskCompletionSource<Packet>();
            return recvTcs.Task;
        }

        /// <summary>
        /// 更新KCP.Update
        /// </summary>
        /// <param name="timeNow"></param>
        public void Update(uint timeNow)
        {
            // 如果还没连接上，发送连接请求
            if (!this.isConnected)
            {
                Connect(timeNow);
                return;
            }

            // 超时断开连接
            if (timeNow - this.lastRecvTime > TimeoutSecs * 1000)
            {
                this.OnError(this, SocketError.Disconnecting);
                return;
            }
            this.kcp.Update(timeNow);
            //下次更新时间
            uint nextUpdateTime = this.kcp.Check(timeNow);
            this.GetService().AddToNextTimeUpdate(nextUpdateTime, this.Id);
        }

    }
}
