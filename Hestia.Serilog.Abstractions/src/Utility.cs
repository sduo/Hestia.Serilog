using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Hestia.Serilog
{ 
    public static class Utility
    {
        public static readonly string AppName = Assembly.GetCallingAssembly()?.GetName()?.Name ?? AppDomain.CurrentDomain.FriendlyName;

        public static readonly ITextFormatter JsonFormatter = new JsonFormatter();
        public static readonly ITextFormatter TextFormatter = new MessageTemplateTextFormatter(string.Join("{NewLine}", "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}", "{Properties}", "{Exception}"), null);

        public static string RenderLogEventToString(LogEvent @event, ITextFormatter formatter)
        {
            if(@event is null) { return null; }
            var fmt = formatter ?? new JsonFormatter();
            using var sw = new StringWriter();
            fmt.Format(@event, sw);
            return sw.ToString();
        }

        public static string RenderLogEventPropertiesToJson(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
        {
            if(properties is null) { return null; }
            var fmt = new JsonValueFormatter();
            using var sw = new StringWriter();
            fmt.Format(new StructureValue(properties.Select(x=>new LogEventProperty(x.Key, x.Value))), sw);
            return sw.ToString();
        }
    }
}
