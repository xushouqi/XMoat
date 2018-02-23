using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace XMoat.Common
{
    public class TChannel : AChannel
    {
        private readonly TcpClient tcpClient;
        private NetworkStream tcpStream;

        private readonly CircularBuffer recvBuffer = new CircularBuffer();
        private readonly CircularBuffer sendBuffer = new CircularBuffer();

        private bool isSending;
        private readonly PacketParser parser;
        private bool isConnected;
        private TaskCompletionSource<Packet> recvTcs;

        public TChannel(TcpClient tcpClient, IPEndPoint ipEndPoint, TService service) : base(service)
        {
            this.tcpClient = tcpClient;
            this.parser = new PacketParser(this.recvBuffer);
            this.RemoteAddress = ipEndPoint;

            //发起连接
            //if (ctype == ChannelType.Connect)
            //{
            //    this.ConnectAsync(ipEndPoint);
            //}
            ////接受连接
            //else if (ctype == ChannelType.Accept)
            //{
            //    this.OnConnected();
            //}
        }

        public override void Dispose()
        {
            if (this.Id == 0)
                return;

            base.Dispose();

            if (this.tcpStream != null)
            {
                tcpStream.Close();
                tcpStream = null;
            }
            this.tcpClient.Close();
        }

        /// <summary>
        /// 发起连接
        /// </summary>
        /// <param name="ipEndPoint"></param>
        public async Task ConnectAsync(IPEndPoint ipEndPoint)
        {
            try
            {
                await this.tcpClient.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);
                OnConnected();
            }
            catch (SocketException e)
            {
                Log.Error($"connect error: {e.SocketErrorCode}");
                this.OnError(this, e.SocketErrorCode);
            }
            catch (Exception e)
            {
                Log.Error($"connect error: {ipEndPoint} {e}");
                this.OnError(this, SocketError.SocketError);
            }
        }

        /// <summary>
        /// 连接建立
        /// </summary>
        public void OnConnected()
        {
            Log.Debug($"TChannel.OnConnected: channelId={this.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            this.isConnected = true;
            this.tcpStream = this.tcpClient.GetStream();
            this.StartSendAsync().Wait();
            this.StartRecvAsync();
        }

        private async Task StartSendAsync()
        {
            try
            {
                //已disposed
                if (this.Id == 0)
                    return;

                // 如果正在发送中,不需要再次发送
                if (this.isSending)
                    return;

                // 没有数据需要发送
                if (this.sendBuffer.TotalSize == 0)
                    return;

                Log.Debug($"TChannel.StartSendAsync: channelId={this.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                while (this.Id > 0)
                {
                    int totalSize = this.sendBuffer.TotalSize;
                    //缓存的数据已发完，结束
                    if (totalSize == 0)
                    {
                        this.isSending = false;
                        return;
                    }

                    this.isSending = true;

                    int sendSize = sendBuffer.ChunkSize - this.sendBuffer.FirstIndex;
                    if (sendSize > totalSize)
                        sendSize = totalSize;

                    await this.tcpStream.WriteAsync(this.sendBuffer.First, this.sendBuffer.FirstIndex, sendSize);
                    Log.Debug($"TChannel.WriteAsync: channelId={this.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                    this.sendBuffer.FirstIndex += sendSize;
                    //当前块已发完
                    if (this.sendBuffer.FirstIndex == sendBuffer.ChunkSize)
                    {
                        this.sendBuffer.FirstIndex = 0;
                        this.sendBuffer.RemoveFirst();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                this.OnError(this, SocketError.SocketError);
            }
        }

        private async void StartRecvAsync()
        {
            try
            {
                Log.Debug($"TChannel.StartRecvAsync: channelId={this.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                while (this.Id > 0)
                {
                    //当前缓冲块剩余空间
                    int size = this.recvBuffer.ChunkSize - this.recvBuffer.LastIndex;
                    //读取数据
                    int n = await this.tcpStream.ReadAsync(this.recvBuffer.Last, this.recvBuffer.LastIndex, size);
                    //连接关闭
                    if (n == 0)
                    {
                        this.OnError(this, SocketError.NetworkReset);
                        return;
                    }

                    this.recvBuffer.LastIndex += n;
                    //当前缓冲块已满，新增一个
                    if (this.recvBuffer.LastIndex == this.recvBuffer.ChunkSize)
                    {
                        this.recvBuffer.AddLast();
                        this.recvBuffer.LastIndex = 0;
                    }

                    if (this.recvTcs != null)
                    {
                        bool isOK = this.parser.Parse();
                        //此包的数据已读取完毕
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
            catch (ObjectDisposedException e)
            {
                Log.Warning(e.ToString());
            }
            catch (IOException e)
            {
                Log.Warning(e.ToString());
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                this.OnError(this, SocketError.SocketError);
            }
        }

        public override Task<Packet> Recv()
        {
            if (this.Id == 0)
            {
                throw new Exception("TChannel Disposed, can't receive!!!");
            }

            bool isOK = this.parser.Parse();
            //直接把当前准备好的包取走
            if (isOK)
            {
                Packet packet = this.parser.GetPacket();
                return Task.FromResult(packet);
            }
            //等待异步回调（来自StartRecvAsync）
            recvTcs = new TaskCompletionSource<Packet>();
            return recvTcs.Task;
        }

        public override void Send(byte[] buffer)
        {
            if (this.Id == 0)
            {
                throw new Exception("TChannel Disposed, can't send!!!");
            }
            byte[] size = BitConverter.GetBytes((ushort)buffer.Length);
            this.sendBuffer.SendTo(size);
            this.sendBuffer.SendTo(buffer);
            if (this.isConnected)
                this.StartSendAsync();
        }

        public override void Send(List<byte[]> buffers)
        {
            if (this.Id == 0)
            {
                throw new Exception("TChannel Disposed, can't send!!!");
            }
            ushort size = (ushort)buffers.Select(b => b.Length).Sum();
            byte[] sizeBuffer = BitConverter.GetBytes(size);
            this.sendBuffer.SendTo(sizeBuffer);
            foreach (byte[] buffer in buffers)
            {
                this.sendBuffer.SendTo(buffer);
            }
            if (this.isConnected)
                this.StartSendAsync();
        }
    }
}
