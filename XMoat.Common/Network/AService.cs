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

		public abstract Task<AChannel> AcceptChannel();

		public abstract AChannel ConnectChannel(IPEndPoint ipEndPoint);

		public abstract void Remove(uint channelId);

		public abstract void Update();

		public abstract void Dispose();
	}
}