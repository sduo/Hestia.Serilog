using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using System.Collections.Generic;

namespace Hestia.Serilog
{
    public sealed class SelfLogSink(ITextFormatter formatter = null) : ChainSink(null)
    {
        public ITextFormatter Formatter {get;init; } = formatter ?? Utility.JsonFormatter;

        protected override void Write(IReadOnlyCollection<LogEvent> events)
        {
            foreach (var @event in events)
            {
                var message = Utility.RenderLogEventToString(@event, Formatter);
                if (string.IsNullOrEmpty(message)) { continue; }
                SelfLog.WriteLine(message);
            }            
        }
    }
}
