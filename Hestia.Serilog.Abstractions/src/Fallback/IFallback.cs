using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hestia.Serilog.Fallback
{
    public interface IFallback
    {
        Task ExecuteAsync(IReadOnlyList<Exception> errors, IReadOnlyCollection<LogEvent> events);
    }
}
