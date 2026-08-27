using System;
using System.Linq;
using System.Text;

namespace Hestia.Serilog.Sinks.AliCloud.SLS
{
    internal static partial class Extensions
    {
        extension(Log log)
        {
            private static string ToHex(string source, Encoding encoding = null)
            {
                if(string.IsNullOrEmpty(source)) { return string.Empty; }
                return Convert.ToHexString((encoding ?? Encoding.UTF8).GetBytes(source));
            }

            internal string BuildLookupKey() 
            {
                var shard = ToHex(log.Shard);
                var tags = (log.Tags?.Count > 0) ? string.Join('&', log.Tags.Select(x=>string.Join('=', ToHex(x.Key), ToHex(x.Value))).OrderBy(x => x)) : string.Empty;
                return string.Join('|', shard, tags);
            }
        }
    }
}
