using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hestia.Serilog.Fallback
{
    public sealed class LocalFileFallback(string name,  string error = "error", string[] columns = null, char separator = ',', char prefix = '"', char postfix='"', Func<string, DateTimeOffset, string> rolling = null , Func<string, DateTimeOffset, string> title = null, Encoding encoding = null): IFallback
    {
        public static readonly Func<string, DateTimeOffset, string> DefaultRolling = (name, ts) => $"[{name}]_{ts:yyyyMMdd}";
        private readonly Encoding Encoding = encoding ?? IFallback.DefaultEncoding;
        private readonly Func<string, DateTimeOffset, string> Title = title ?? IFallback.DefaultTitle;
        private readonly Func<string, DateTimeOffset, string> Rolling = rolling ?? DefaultRolling;
#if NET8_0_OR_GREATER
        private readonly IReadOnlyCollection<string> Columns = columns?.AsReadOnly() ?? Utility.LogEventPicker.Keys as IReadOnlyCollection<string>;
#else
        private readonly IReadOnlyCollection<string> Columns = columns is null ? Utility.LogEventPicker.Keys : new ReadOnlyCollection<string>(columns);
#endif
        

        private async Task ErrorAsync(DateTimeOffset ts, IReadOnlyList<Exception> errors)
        {
            var file = $"{Rolling.Invoke(error,ts)}.log" ;
            using var fs = File.Open(file, FileMode.OpenOrCreate, FileAccess.Write);
            using var sw = new StreamWriter(fs, Encoding);
            var title = Title.Invoke(error, ts);
            if (!string.IsNullOrEmpty(title))
            {
                await sw.WriteLineAsync(title);
            }
            foreach (var error in errors) {
                await sw.WriteLineAsync(error.Message);
                await sw.WriteLineAsync(error.GetBaseException().Message);
                await sw.WriteLineAsync(error.StackTrace);
            }            
        }

        private string FormatLine(IReadOnlyCollection<string> fields)
        {
            if(fields is null || fields.Count == 0) { return string.Empty; }
            return string.Join(separator, fields.Select(x => string.Concat(prefix, x, postfix)));
        }

        // private string EmptyLine(int length) => FormatLine(new string[length]);

        private async Task LogAsync(DateTimeOffset ts, IReadOnlyCollection<LogEvent> events)
        {
            var file = $"{Rolling.Invoke(name,ts)}.csv";
            var title = File.Exists(file) ? FormatLine(Columns) : null;
            using var fs = File.Open(file, FileMode.OpenOrCreate, FileAccess.Write);
            using var sw = new StreamWriter(fs, Encoding);
            if (!string.IsNullOrEmpty(title)) { await sw.WriteLineAsync(title); }
            foreach (var @event in events)
            {
                var dynamics = Utility.BuildLogEventDictionary(@event, Columns.ToDictionary(x => x, x => x));
                var line = FormatLine(dynamics.Values);
                await sw.WriteLineAsync(line);
            }
        }

        public async Task ExecuteAsync(IReadOnlyList<Exception> errors, IReadOnlyCollection<LogEvent> events)
        {
            var ts = DateTimeOffset.Now;
            await ErrorAsync(ts, errors);
            await LogAsync(ts, events);
        }
    }
}
