using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualSenseAPI.Util
{
    internal static class ByteConverterExtensions
    {
        public static float ToSignedFloat(this byte b)
        {
            return (b / 255.0f - 0.5f) * 2.0f;
        }

        public static float ToUnsignedFloat(this byte b)
        {
            return b / 255.0f;
        }

        // todo - remove when done debugging things
        public static string ToBitString(this byte b)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 7; i >= 0; i--)
            {
                builder.Append((b >> i) % 2);
            }
            return builder.ToString();
        }

        public static bool HasFlag(this byte b, byte flag)
        {
            return (b & flag) == flag;
        }

        public static byte UnsignedToByte(this float f)
        {
            return (byte)(Math.Clamp(f, 0, 1) * 255);
        }
    }
}
