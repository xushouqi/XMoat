using System;
using System.Net;
using System.Threading.Tasks;

namespace XMoat.Common
{
	public enum NetworkProtocol
	{
		TCP,
		KCP,
	}

	public abstract class AService: IDisposable
	{
		public abstract AChannel GetChannel(uint id);

		public abstract Task<AChannel> AcceptChannelAsync();

		public abstract Task<AChannel> ConnectChannelAsync(IPEndPoint ipEndPoint);

		public abstract void RemoveChannel(uint channelId);

		public abstract void Update();

		public abstract void Dispose();
	}
}