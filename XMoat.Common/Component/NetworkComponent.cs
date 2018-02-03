using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;

namespace XMoat.Common
{
    public class NetworkComponent : Component
    {
        private AService service;
        public AService Service { get { return service; } }

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
                        //this.service = new TService(ipEndPoint);
                        break;
                    case NetworkProtocol.KCP:
                        this.service = new XService(ipEndPoint);
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
            while (true)
            {
                //if (this.Id == 0)
                //{
                //    return;
                //}

                await this.Accept();
            }
        }

        public virtual async Task<AChannel> Accept()
        {
            AChannel channel = await this.Service.AcceptChannel();
            channel.ErrorCallback += (c, e) => { Log.Error($"ChannelError: {c.Id}: {e.ToString()}"); };
            //Session session = EntityFactory.Create<Session, NetworkComponent, AChannel>(this, channel);
            //channel.ErrorCallback += (c, e) => { this.Remove(session.Id); };
            //this.sessions.Add(session.Id, session);
            return channel;
        }



    }
}
