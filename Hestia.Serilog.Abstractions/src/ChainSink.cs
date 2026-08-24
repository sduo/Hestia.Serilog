using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Hestia.Serilog
{
    public abstract class ChainSink(ChainSink next = null) : ILogEventSink, IBatchedLogEventSink
    {
        protected virtual LogEvent ExceptionToLogEvent(Exception ex)
        {
            if (Activity.Current is null)
            {
                return new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, ex, MessageTemplate.Empty, []);
            }
            return new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, ex, MessageTemplate.Empty, [], Activity.Current.TraceId, Activity.Current.SpanId);
        }

        protected abstract void Write(IReadOnlyCollection<LogEvent> events);

        private LogEvent SafeExceptionToLogEvent(Exception ex)
        {
            if (ex is null) { return null; }
            try { return ExceptionToLogEvent(ex); } catch { return null; }
        }

        private void SafeWrite(IReadOnlyCollection<LogEvent> events)
        {
            if(events is null || events.Count == 0) { return; }
            try
            {
                Write(events);
            }
            catch (Exception ex)
            {
                next?.Execute(events, ex);
            }
        }

        public void Execute(IReadOnlyCollection<LogEvent> events, Exception ex)
        {
            if(events is null && ex is null) { return ; }
            var error = SafeExceptionToLogEvent(ex);
            if (events is null || events .Count == 0) { 
                if(error is not null)
                {
                    SafeWrite([error]);
                }
            }
            else
            {
                if(error is null)
                {
                    SafeWrite(events);
                }
                else
                {
                    var list = new List<LogEvent>(events.Count + 1);
                    list.AddRange(events);
                    list.Add(error);
                    SafeWrite(list);
                }
            }            
        }

        public Task EmitBatchAsync(IReadOnlyCollection<LogEvent> events)
        {
            return Task.Run(() => SafeWrite(events));
        }

        public void Emit(LogEvent @event)
        {
            SafeWrite([@event]);
        }
    }
}
