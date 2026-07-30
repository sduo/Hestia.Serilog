using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hestia.Serilog.Fallback
{
    public sealed class LocalFileFallback(string name,  string error = "error", string[] columns = null, char separator = ',', char prefix = '"', char postfix='"',string rolling = "yyyyMMdd", Encoding encoding = null): IFallback
    {
        private readonly Encoding encoding = encoding ?? Encoding.UTF8;
        private readonly string[] columns = columns ?? [Columns.Timestamp, Columns.Level, Columns.TraceId, Columns.SpanId, Columns.Message, Columns.Template, Columns.Properties, Columns.Exception, Columns.ExceptionBase, Columns.ExceptionStackTrace];
        private readonly Func<string,DateTimeOffset,string> fmt = (name,ts) => string.IsNullOrEmpty(rolling) ? name : string.Join('.', name, ts.ToString(rolling));

        private async Task ErrorAsync(DateTimeOffset ts, IReadOnlyList<Exception> errors)
        {
            var file = $"{fmt.Invoke(error,ts)}.log" ;
            using var fs = File.Open(file, FileMode.OpenOrCreate, FileAccess.Write);
            using var sw = new StreamWriter(fs, encoding);
            await sw.WriteLineAsync($"{name} - {ts:yyyy-MM-dd HH:mm:ss.fff}");
            foreach (var error in errors) {
                await sw.WriteLineAsync(error.Message);
                await sw.WriteLineAsync(error.GetBaseException().Message);
                await sw.WriteLineAsync(error.StackTrace);
            }            
        }

        private string FormatLine(string[] fields)
        {
            if(fields is null || fields.Length == 0) { return string.Empty; }
            return string.Join(separator, fields.Select(x => string.Concat(prefix, x, postfix)));
        }

        // private string EmptyLine(int length) => FormatLine(new string[length]);

        private async Task LogAsync(DateTimeOffset ts, LogEvent[] events)
        {
            var file = $"{fmt.Invoke(name,ts)}.csv";
            var title = File.Exists(file) ? FormatLine(columns) : null;
            using var fs = File.Open(file, FileMode.OpenOrCreate, FileAccess.Write);
            using var sw = new StreamWriter(fs, encoding);
            if (!string.IsNullOrEmpty(title)) { await sw.WriteLineAsync(title); }
            foreach (var @event in events)
            {
                var dynamics = new Dictionary<string, string>() {
                    { Columns.Timestamp, $"{@event.Timestamp:yyyy-MM-dd HH:mm:ss.fff}" },
                    { Columns.Level, $"{@event.Level}" },
                    { Columns.TraceId, @event.TraceId?.ToHexString() ?? string.Empty },
                    { Columns.SpanId, @event.SpanId?.ToHexString() ?? string.Empty },
                    { Columns.Message, @event.RenderMessage() ?? string.Empty },
                    { Columns.Template, @event.MessageTemplate.Text },
                    { Columns.Properties, Utility.FormatProperties(@event.Properties) },
                    { Columns.Exception, @event.Exception?.Message ?? string.Empty },
                    { Columns.ExceptionBase, @event.Exception?.GetBaseException().Message ?? string.Empty },
                    { Columns.ExceptionStackTrace, @event.Exception?.StackTrace ?? string.Empty },
                };
                var line = FormatLine(columns.Select(x => dynamics.ContainsKey(x) ? dynamics[x] : string.Empty).ToArray());
                await sw.WriteLineAsync(line);
            }
        }

        public async Task ExecuteAsync(IReadOnlyList<Exception> errors, LogEvent[] events)
        {
            var ts = DateTimeOffset.Now;
            await ErrorAsync(ts, errors);
            await LogAsync(ts, events);
        }
    }
}
