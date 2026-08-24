using Serilog.Events;
using Serilog.Formatting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Hestia.Serilog
{
    public sealed class LocalFileChainSink(string name, ITextFormatter formatter, ChainSink next = null) : ChainSink(next)
    {
        public LocalFileChainSink(string name = null, ITextFormatter formatter = null) : this(name, formatter, new ConsoleChainSink()) { }

        public string Name { get; init; } = string.IsNullOrEmpty(name) ? Utility.AppName : name;

        public Encoding Encoding { get; init; } = Encoding.UTF8;
        public string Rolling { get; init; } = "yyyyMMdd";
        public ITextFormatter Formatter { get; init; } = formatter ?? Utility.TextFormatter;      

        protected override async void Write(IReadOnlyCollection<LogEvent> events)
        {
            var lookup = events.ToLookup((x) => { 
                return string.IsNullOrEmpty(Rolling) ? $"{Name}.log" : $"{Name}_{x.Timestamp.ToString(Rolling, CultureInfo.InvariantCulture)}.log";
            });
            foreach(var batch in lookup)
            {
                var log = Path.IsPathRooted(batch.Key) ? batch.Key : Path.Combine(AppContext.BaseDirectory, batch.Key);
                var folder = Path.GetDirectoryName(log);
                if (!string.IsNullOrEmpty(folder)) { Directory.CreateDirectory(folder); }
                using var fs = File.Open(log, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs, Encoding);
                foreach(var @event in batch)
                {
                    var message = Utility.RenderLogEventToString(@event, Formatter);
                    if (string.IsNullOrEmpty(message)) { continue; }
                    await sw.WriteLineAsync(message);
                }
            }
        }
    }
}
