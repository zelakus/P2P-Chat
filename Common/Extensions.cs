using System;
using System.Net;

namespace Common
{
    public static class Extensions
    {
        public static long ToLong(this IPEndPoint @this)
        {
            var ip = @this.Address.GetAddressBytes();
            if (ip.Length != 4)
                throw new Exception("Only IPv4 is supported.");

            long value = BitConverter.ToInt32(ip, 0);
            value <<= 16;
            value |= (ushort)(@this.Port & 0xFFFF);
            return value;
        }
    }
}
