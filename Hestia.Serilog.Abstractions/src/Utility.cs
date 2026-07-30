using Microsoft.VisualBasic;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Principal;
using static Hestia.Serilog.Utility;

namespace Hestia.Serilog
{ 
    public static class Utility
    {
        public static class Keys
        {
            public const string Timestamp = "Timestamp";
            public const string Level = "Level";
            public const string TraceId = "TraceId";
            public const string SpanId = "SpanId";
            public const string Message = "Message";
            public const string Template = "Template";
            public const string Properties = "Properties";
            public const string Exception = "Exception";
            public const string ExceptionBase = "ExceptionBase";
            public const string ExceptionStackTrace = "ExceptionStackTrace";
            public const string PropertyPrefix = "Property.";
        }

        private static ReadOnlyDictionary<string, Func<LogEvent, string>> LogEventPicker = (new() {
            { Keys.Timestamp,  @event => $"{@event.Timestamp:yyyy-MM-dd HH:mm:ss.fff}" },
            { Keys.Level, @event => $"{@event.Level}" },
            { Keys.TraceId, @event =>  @event.TraceId?.ToHexString() ?? string.Empty },
            { Keys.SpanId, @event => @event.SpanId?.ToHexString() ?? string.Empty },
            { Keys.Message, @event => @event.RenderMessage() ?? string.Empty },
            { Keys.Template, @event => @event.MessageTemplate.Text },
            { Keys.Properties, @event => Utility.FormatProperties(@event.Properties) },
            { Keys.Exception, @event => @event.Exception?.Message ?? string.Empty },
            { Keys.ExceptionBase, @event => @event.Exception?.GetBaseException().Message ?? string.Empty },
            { Keys.ExceptionStackTrace, @event => @event.Exception?.StackTrace ?? string.Empty }
        }).AsReadOnly();

        public static string FormatProperty(LogEventPropertyValue property)
        {
            if (property is null) { return null; }
            var formatter = new JsonValueFormatter();
            using var sw = new StringWriter();
            formatter.Format(property, sw);
            var value = sw.ToString();
            return value;
        }

        public static string FormatProperties(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
        {
            if (properties is null) { return null; }
            if (properties.Count == 0) { return string.Empty; }        
            return FormatProperty(new StructureValue(properties.Select(p => new LogEventProperty(p.Key, p.Value))));
        }

        public static string FormatEvent(LogEvent @event)
        {
            if (@event is null) { return null; }
            var formatter = new JsonFormatter();
            using var sw = new StringWriter();
            formatter.Format(@event, sw);
            var message = sw.ToString();
            return message;
        }        

        public static Dictionary<string, string> BuildLogEventDictionary(LogEvent @event, IReadOnlyDictionary<string,string> map)
        {            
            if(map is null || map.Count == 0)
            {
                return LogEventPicker.ToDictionary(x => x.Key, x => x.Value.Invoke(@event));
            }

            var mapped = new Dictionary<string,string>();
            foreach(var kv in map)
            {
                if (LogEventPicker.TryGetValue(kv.Key, out var func))
                {
                    mapped.TryAdd(kv.Value, func.Invoke(@event));
                }
            }
            return mapped;
        }
    }
}
