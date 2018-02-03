using System.IO;
using LZ4;

namespace XMoat.Common
{
	public static class ZipHelper
	{
		public static byte[] Compress(byte[] content)
        {
            var buf = LZ4Codec.Wrap(content);
            return buf;
		}

		public static byte[] Decompress(byte[] content)
		{
			return Decompress(content, 0, content.Length);
		}

		public static byte[] Decompress(byte[] content, int offset, int count)
		{
            var buf = LZ4Codec.Unwrap(content, offset);
            return buf;
		}
	}
}

/*
using System.IO;
using System.IO.Compression;

namespace XMoat.Common
{
	public static class ZipHelper
	{
		public static byte[] Compress(byte[] content)
		{
			using (MemoryStream ms = new MemoryStream())
			using (DeflateStream stream = new DeflateStream(ms, CompressionMode.Compress, true))
			{
				stream.Write(content, 0, content.Length);
				return ms.ToArray();
			}
		}

		public static byte[] Decompress(byte[] content)
		{
			return Decompress(content, 0, content.Length);
		}

		public static byte[] Decompress(byte[] content, int offset, int count)
		{
			using (MemoryStream ms = new MemoryStream())
			using (DeflateStream stream = new DeflateStream(new MemoryStream(content, offset, count), CompressionMode.Decompress, true))
			{
				byte[] buffer = new byte[1024];
				while (true)
				{
					int bytesRead = stream.Read(buffer, 0, 1024);
					if (bytesRead == 0)
					{
						break;
					}
					ms.Write(buffer, 0, bytesRead);
				}
				return ms.ToArray();
			}
		}
	}
}
*/