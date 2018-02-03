namespace XMoat.Common
{
	public static class IdGenerater
	{
		public static uint AppId { private get; set; }

		private static ushort value;

        public static long GenerateId()
        {
            var time = TimeHelper.ClientNowSeconds();

            return (AppId << 48) + (time << 16) + ++value;
        }
    }
}