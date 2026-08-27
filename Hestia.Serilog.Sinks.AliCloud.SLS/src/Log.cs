using System.Collections.Generic;

namespace Hestia.Serilog.Sinks.AliCloud.SLS
{
    public record Log
    {
        public string Shard { get; init; }
        public IReadOnlyDictionary<string, string> Tags { get; init; }
        public IReadOnlyDictionary<string, string> Contents { get; init; }

        public uint Timestamp {  get; init; }
    }
}
