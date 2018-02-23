using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace XMoat.Common
{
    public class NetworkComponent : Component
    {
        private AService service;
        public AService Service { get { return service; } }

        private readonly Dictionary<long, Session> sessions = new Dictionary<long, Session>();

        /// <summary>
        /// 服务端绑定端口
        /// </summary>
        /// <param name="protocol"></param>
        /// <param name="ipEndPoint"></param>
        public void Awake(NetworkProtocol protocol, IPEndPoint ipEndPoint)
        {
            try
            {
                switch (protocol)
                {
                    case NetworkProtocol.TCP:
                        this.service = new TService(ipEndPoint);
                        break;
                    case NetworkProtocol.KCP:
                        this.service = new KService(ipEndPoint);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                this.StartAccept();
            }
            catch (Exception e)
            {
                throw new Exception($"{ipEndPoint}", e);
            }
        }


        private async void StartAccept()
        {
            while (this.Id > 0)
            {
                await this.Accept();
            }
        }

        public virtual async Task<AChannel> Accept()
        {
            AChannel channel = await this.Service.AcceptChannelAsync();
            Session session = new Session(this, channel);
            this.AddSession(session);
            channel.ErrorCallback += (c, e) => { this.RemoveSession(session.Id); };
            channel.ErrorCallback += OnNetworkError;
            return channel;
        }

        private void AddSession(Session session)
        {
            this.sessions.Add(session.Id, session);
        }
        public Session GetSession(long id)
        {
            Session session;
            this.sessions.TryGetValue(id, out session);
            return session;
        }
        public virtual void RemoveSession(long id)
        {
            Session session;
            if (!this.sessions.TryGetValue(id, out session))
            {
                return;
            }
            this.sessions.Remove(id);
            session.Dispose();
        }


        private void OnNetworkError(AChannel channel, SocketError error)
        {
            Log.Error($"ChannelError: {channel.Id}: {error.ToString()}");
        }

        public void Update()
        {
            if (this.Service == null)
            {
                return;
            }
            this.Service.Update();
        }

        public override void Dispose()
        {
            if (this.Id == 0)
            {
                return;
            }

            base.Dispose();

            foreach (Session session in this.sessions.Values.ToArray())
            {
                session.Dispose();
            }

            this.Service.Dispose();
        }
    }
}
