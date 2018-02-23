namespace XMoat.Common
{
	public static class IdGenerater
	{
		public static uint AppId { private get; set; }

		private static ushort value;

        public static long GenerateId64()
        {
            var time = TimeHelper.ClientNowSeconds();

            return (AppId << 48) + (time << 16) + ++value;
        }

        private static int Id32 = 0;
        public static uint GenerateId32()
        {
            return (uint)System.Threading.Interlocked.Increment(ref Id32);
        }
    }
}