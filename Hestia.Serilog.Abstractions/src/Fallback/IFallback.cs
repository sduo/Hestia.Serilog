using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hestia.Serilog.Fallback
{
    public interface IFallback
    {
        Task ExecuteAsync(IReadOnlyList<Exception> errors, IReadOnlyCollection<LogEvent> events);
        static readonly Func<string, DateTimeOffset, string> DefaultTitle = (name, ts) => $"[{name}] {ts:yyyy-MM-dd HH:mm:ss.fff}";        
        static readonly Encoding DefaultEncoding = Encoding.UTF8;
    }
}
