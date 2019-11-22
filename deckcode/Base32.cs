using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LoR
{
    class Base32
    {
        private static char[]                           CHARS     = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();
        private static readonly int                     MASK      = CHARS.Length - 1;
        private static readonly int                     SHIFT     = NumberOfTrailingZeros(CHARS.Length);
        private static readonly Dictionary<char, int>   CHAR_MAP  = new Dictionary<char, int>();

        static Base32()
        {
            for(int i = 0; i < CHARS.Length; i++)
            {
                CHAR_MAP[CHARS[i]] = i;
            }
        }


        private static int NumberOfTrailingZeros(int i)
        {
            int y;
            if(i == 0) return 32;
            int n = 31;
            y = i << 16; if(y != 0) { n = n - 16; i = y; }
            y = i << 8; if(y != 0) { n = n - 8; i = y; }
            y = i << 4; if(y != 0) { n = n - 4; i = y; }
            y = i << 2; if(y != 0) { n = n - 2; i = y; }
            return n - (int) ((uint) (i << 1) >> 31);
        }

        public static byte[] Decode(String code)
        {
            // remove padding (=) and separators (-)
            code = Regex.Replace(code, "[=-]*", "").ToUpper();

            if(code.Length == 0)
            {
                return new byte[] { 0 };
            }

            int    encodedLength = code.Length;
            int    outLength     = encodedLength * SHIFT / 8;
            byte[] result        = new byte[outLength];

            int buffer   = 0;
            int next     = 0;
            int bitsLeft = 0;

            foreach(char c in code.ToCharArray())
            {
                if(!CHAR_MAP.ContainsKey(c))
                {
                    throw new Exception("Illegal character: " + c);
                }

                buffer <<= SHIFT;
                buffer |= CHAR_MAP[c] & MASK;
                bitsLeft += SHIFT;
                if(bitsLeft >= 8)
                {
                    result[next++] = (byte) (buffer >> (bitsLeft - 8));
                    bitsLeft -= 8;
                }

            }

            return result;
        }

        public static string Encode(byte[] data, bool pad = false)
        {
            if(data.Length == 0)
            {
                return "";
            }

            if(data.Length >= (1 << 28))
            {
                throw new Exception("Array is too long for this");
            }

            int           outputLength = (data.Length * 8 + SHIFT - 1) / SHIFT;
            StringBuilder result       = new StringBuilder(outputLength);

            int buffer   = data[0];
            int next     = 1;
            int bitsLeft = 8;
            while(bitsLeft > 0 || next < data.Length)
            {
                if(bitsLeft < SHIFT)
                {
                    if(next < data.Length)
                    {
                        buffer <<= 8;
                        buffer |= (data[next++] & 0xff);
                        bitsLeft += 8;
                    }
                    else
                    {
                        int padInner = SHIFT - bitsLeft;
                        buffer <<= padInner;
                        bitsLeft += padInner;
                    }
                }

                int index = MASK & (buffer >> (bitsLeft - SHIFT));
                bitsLeft -= SHIFT;
                result.Append(CHARS[index]);
            }

            if(pad)
            {
                int padding = 8 - (result.Length % 8);
                if(padding > 0) { result.Append(new string('=', padding == 8 ? 0 : padding)); }
            }

            return result.ToString();
        }
    }
}
