using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;

namespace XMoat.Common
{
    public enum KcpProtocalType
    {
        SYN = 1,
        ACK = 2,
        FIN = 3,
        HEART = 4,
        HEARTACK = 5,
    }

    public class KService : AService
    {
        public uint IdGenerater = 10000;
        public UdpClient socket;
        public uint TimeNow;

        private TaskCompletionSource<AChannel> acceptTcs;
        private TaskCompletionSource<AChannel> connectTcs;

        private readonly Dictionary<uint, KChannel> idChannels = new Dictionary<uint, KChannel>();

        // 下次时间更新的channel
        private readonly MultiMap<long, uint> timerMap = new MultiMap<long, uint>();
        // 下帧要更新的channel
        private readonly HashSet<uint> updateChannels = new HashSet<uint>();
        //待删除的channel
        private readonly Queue<uint> removedChannels = new Queue<uint>();

        public KService(IPEndPoint ipEndPoint)
        {
            this.TimeNow = (uint)TimeHelper.Now();
            this.socket = new UdpClient(ipEndPoint);

            const uint IOC_IN = 0x80000000;
            const uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            //解决问题"远程主机关闭一个现有连接"：Soket UDP 在进行发送的时候,导致异常,却在接收函数引发异常.
            this.socket.Client.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);

            this.StartRecv();

            //test
            this.StartUpdate();
        }

        private async void StartRecv()
        {
            while (true)
            {
                if (this.socket == null)
                    return;

                //接收数据
                UdpReceiveResult udpReceiveResult;
                try
                {
                    udpReceiveResult = await this.socket.ReceiveAsync();
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
                    continue;
                }

                int messageLength = udpReceiveResult.Buffer.Length;
                // 长度小于4，不是正常的消息
                if (messageLength < 4)
                    continue;

                //数据包的类型/或channelId
                uint ptype = BitConverter.ToUInt32(udpReceiveResult.Buffer, 0);
                Log.Info($"StartRecv.ReceiveAsync: thread={System.Threading.Thread.CurrentThread.ManagedThreadId}, ptype={ptype}, messageLength={messageLength}");

                switch ((KcpProtocalType)ptype)
                {
                    case KcpProtocalType.SYN:
                        // 长度!=8，不是accpet消息
                        if (messageLength != 8)
                            break;
                        //尚未开始等待连接
                        if (this.acceptTcs == null)
                            return;

                        //发送者的ID
                        uint remoteId = BitConverter.ToUInt32(udpReceiveResult.Buffer, 4);

                        KChannel sChannel;
                        // 如果已经连接上,则重新响应请求
                        if (this.idChannels.TryGetValue(remoteId, out sChannel))
                        {
                            sChannel.HandleAccept(remoteId);
                            return;
                        }

                        TaskCompletionSource<AChannel> tcs = this.acceptTcs;
                        this.acceptTcs = null;

                        //创建新的channel
                        sChannel = new KChannel(ChannelType.Accept, this, ++this.IdGenerater, udpReceiveResult.RemoteEndPoint, socket);
                        //如已有同ID的channel，销毁旧的
                        if (this.idChannels.TryGetValue(sChannel.Id, out KChannel oldChannel))
                        {
                            this.idChannels.Remove(oldChannel.Id);
                            oldChannel.Dispose();
                        }
                        //记录channel
                        this.idChannels[sChannel.Id] = sChannel;
                        Log.Debug($"StartRecv.KcpProtocalType.SYN: channelId={sChannel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");

                        //向remote返回ACK
                        sChannel.HandleAccept(remoteId);

                        //异步回调
                        tcs.SetResult(sChannel);

                        //test:开始接收
                        //sChannel.StartRecvAsync();
                        break;
                    case KcpProtocalType.ACK:
                        // 长度!=12，不是connect消息
                        if (messageLength != 12)
                            break;

                        uint channelId = BitConverter.ToUInt32(udpReceiveResult.Buffer, 4);
                        uint tmpId = BitConverter.ToUInt32(udpReceiveResult.Buffer, 8);
                        Log.Debug($"StartRecv.KcpProtocalType.ACK: tmpId={tmpId}, channelId={channelId}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");

                        //已有连接，
                        if (this.idChannels.TryGetValue(tmpId, out KChannel aChannel))
                        {
                            //处理chanel
                            aChannel.HandleConnnect(channelId);
                            //移除旧ID的，替换为新ID
                            this.idChannels.Remove(tmpId);
                            this.idChannels[channelId] = aChannel;
                            //异步回调
                            if (connectTcs != null)
                            {
                                connectTcs.SetResult(aChannel);
                                connectTcs = null;
                                Log.Debug($"KService.ConnectChannelAsync.Finish: channelId={aChannel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                            }
                        }
                        break;
                    case KcpProtocalType.FIN:
                        // 长度!= 8，不是DisConnect消息
                        if (messageLength != 8)
                            break;

                        uint closeId = BitConverter.ToUInt32(udpReceiveResult.Buffer, 4);
                        if (this.idChannels.TryGetValue(closeId, out KChannel fChannel))
                        {
                            // 处理chanel
                            this.idChannels.Remove(closeId);
                            fChannel.Dispose();
                            Log.Debug($"StartRecv.KcpProtocalType.FIN: channelId={closeId}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                        }
                        break;
                    case KcpProtocalType.HEART:
                        if (messageLength != 8)
                            break;

                        uint hId = BitConverter.ToUInt32(udpReceiveResult.Buffer, 4);
                        if (this.idChannels.TryGetValue(hId, out KChannel hChannel))
                        {
                            //回应
                            hChannel.HeartbeatAck(this.TimeNow);
                        }
                        break;
                    case KcpProtocalType.HEARTACK:
                        if (messageLength != 8)
                            break;

                        uint haId = BitConverter.ToUInt32(udpReceiveResult.Buffer, 4);
                        if (this.idChannels.TryGetValue(haId, out KChannel haChannel))
                        {
                            haChannel.FinishHeartbeat(this.TimeNow);
                        }
                        break;
                    default:
                        var cid = ptype;
                        if (this.idChannels.TryGetValue(cid, out KChannel rChannel))
                        {
                            // 处理chanel
                            rChannel.HandleRecv(udpReceiveResult.Buffer, this.TimeNow);
                        }
                        break;
                }
            }
        }

        public override async Task<AChannel> AcceptChannelAsync()
        {
            //等待传入连接请求
            acceptTcs = new TaskCompletionSource<AChannel>();
            return await this.acceptTcs.Task;
        }

        /// <summary>
        /// 请求连接服务器
        /// </summary>
        /// <param name="ipEndPoint"></param>
        /// <returns></returns>
        public override Task<AChannel> ConnectChannelAsync(IPEndPoint ipEndPoint)
        {
            if (connectTcs == null)
            {
                connectTcs = new TaskCompletionSource<AChannel>();
                //随机一个临时ID，连接成功后会被服务器替换
                uint channelId = (uint)RandomHelper.RandomNumber(1000, int.MaxValue);
                KChannel channel = new KChannel(ChannelType.Connect, this, channelId, ipEndPoint, this.socket);
                //将相同id的channel销毁???
                KChannel oldChannel;
                if (this.idChannels.TryGetValue(channelId, out oldChannel))
                {
                    this.idChannels.Remove(channelId);
                    oldChannel.Dispose();
                }
                this.idChannels[channelId] = channel;
                Log.Debug($"KService.ConnectChannelAsync.Start: channelId={channel.Id}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                return connectTcs.Task;
            }
            else
                return null;
        }

        public override void Dispose()
        {
            if (this.socket != null)
            {
                this.socket.Close();
                this.socket = null;
            }
        }

        public override AChannel GetChannel(uint channelId)
        {
            KChannel channel;
            this.idChannels.TryGetValue(channelId, out channel);
            return channel;
        }

        public override void RemoveChannel(uint channelId)
        {
            KChannel channel = null;
            if (this.idChannels.TryGetValue(channelId, out channel))
            {
                if (channel != null)
                {
                    this.removedChannels.Enqueue(channelId);
                    channel.Dispose();
                }
            }
        }

        /// <summary>
        /// test: 自循环Update，如有外部调用update，则无需如此
        /// </summary>
        public async void StartUpdate()
        {
            while (true)
            {
                Update();
                var curTime = (uint)TimeHelper.Now();
                //10毫秒update一次
                var delay = Math.Max(1, 10 - (int)(curTime - this.TimeNow));
                await Task.Delay(delay);
            }
        }

        public override void Update()
        { 
            this.TimeNow = (uint)TimeHelper.Now();

            //定时update的channel
            while (this.timerMap.Count > 0)
            {
                var kv = this.timerMap.First();
                //最早的那个尚未到时间
                if (kv.Key > TimeNow)
                    break;

                var timeOutId = kv.Value;
                //需要update的channel添加入列表
                foreach (var id in timeOutId)
                    this.updateChannels.Add(id);

                this.timerMap.Remove(kv.Key);
            }

            //需要update的channel
            foreach (var id in updateChannels)
            {
                if (this.idChannels.TryGetValue(id, out KChannel kChannel))
                {
                    if (kChannel.Id > 0)
                        kChannel.Update(this.TimeNow);
                }
            }
            this.updateChannels.Clear();

            //待删除的channel
            while (this.removedChannels.Count > 0)
            {
                var channelId = this.removedChannels.Dequeue();
                this.idChannels.Remove(channelId);
            }

            //长时间未收过数据的连接发送心跳包
            foreach (var ch in idChannels)
            {
                //仅客户端需要
                if (ch.Value.ChType == ChannelType.Connect)
                    ch.Value.TryHeartbeat(this.TimeNow);
            }
        }

        public void AddToUpdate(uint id)
        {
            this.updateChannels.Add(id);
        }

        public void AddToNextTimeUpdate(long time, uint id)
        {
            this.timerMap.Add(time, id);
        }
    }
}
