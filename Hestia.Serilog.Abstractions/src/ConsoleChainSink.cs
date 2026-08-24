using Serilog.Events;
using Serilog.Formatting;
using System;
using System.Collections.Generic;

namespace Hestia.Serilog
{
    public sealed class ConsoleChainSink(ITextFormatter formatter, ChainSink next = null) : ChainSink(next)
    {
        public bool UseStandardOutputForErrors { get; init; } = false;

        public ITextFormatter Formatter { get; init; } = formatter ?? Utility.TextFormatter;

#if DEBUG
        public ConsoleChainSink(ITextFormatter formatter = null) : this(formatter, new SelfLogSink()) { }
#else
        public ConsoleChainSink(ITextFormatter formatter = null) : this(formatter, null) { }
#endif
        protected override void Write(IReadOnlyCollection<LogEvent> events)
        { 
            foreach (var @event in events)
            {
                var message = Utility.RenderLogEventToString(@event, Formatter);
                if (string.IsNullOrEmpty(message)) { continue; }
                var output = (UseStandardOutputForErrors || (@event.Level < LogEventLevel.Error)) ? Console.Out : Console.Error;
                output.WriteLine(message);
            }
        }
    }
}
