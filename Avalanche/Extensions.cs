using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Avalanche
{
    public static class Extensions
    {
        public static Guid? TryParseGuid(this string str)
        {
            Guid result;
            if (Guid.TryParse(str, out result))
            {
                return result;
            }
            return null;
        }

        public static T? ParseEnum<T>(this string str)
            where T : struct
        {
            try
            {
                var result = (T)Enum.Parse(typeof(T), str);
                if (Enum.IsDefined(typeof(T), result))
                {
                    return result;
                }
            }
            catch { }
            return null;
        }
    }
}