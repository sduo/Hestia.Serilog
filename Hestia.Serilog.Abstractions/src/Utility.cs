using Serilog.Events;
using Serilog.Formatting.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hestia.Serilog
{ 
    public static class Utility
    {
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
    }
}
