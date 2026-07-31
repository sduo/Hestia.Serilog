using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Hestia.Serilog.Fallback
{
    public sealed class ConsoleFallback(string name, TextWriter output, TextWriter error, Func<string,DateTimeOffset,string> title, Action<string> diagnotor) : IFallback 
    {
        private readonly TextWriter Output = output ?? Console.Out;
        private readonly TextWriter Error = error ?? Console.Error;
        private readonly Func<string,DateTimeOffset,string> Title = title ?? IFallback.DefaultTitle;
        private readonly Action<string> Diagnotor = diagnotor;        

        static ConsoleFallback()
        {
            Console.OutputEncoding = IFallback.DefaultEncoding;
        }

        public ConsoleFallback(string name):this (name,Console.Out, Console.Error, null, (message) => { Trace.WriteLine(message); }) { }

        private void TryDiagnose(string message)
        {
            if ( Diagnotor is null) { return; }
            if (string.IsNullOrEmpty(message)) { return; }
            try
            {                
                Diagnotor.Invoke(message);
            }
            catch
            {

            }
        }

        private static string FormatException(Exception error)
        {
            if (error is null) { return null; }
            try
            {
                var line = string.Join(Environment.NewLine, error.Message, error.GetBaseException().Message, string.Empty, error.StackTrace);
                return line;
            }
            catch (Exception ex)
            {
                return string.Join(Environment.NewLine, ex.ToString(),string.Empty,error.ToString());
            }
        }

        private void WriteError(Exception error) => SafeWriteLine(Error,FormatException(error));       

        private void SafeWriteLine(TextWriter writer, string message)
        {
            if(writer is null) { return; }
            if(string.IsNullOrEmpty(message)) { return; }
            try
            {
                writer.WriteLine(message);
            }
            catch(Exception ex)
            {
                TryDiagnose(FormatException(ex));
            }
        }

        private void WriteError(DateTimeOffset ts, IReadOnlyList<Exception> errors)
        {
            if(errors is null || errors.Count == 0) { return; }            
            SafeWriteLine(Error , Title.Invoke(name, ts));
            foreach (var error in errors)
            {
                WriteError(error);
            }
        }

        private void WriteLog(DateTimeOffset ts, IReadOnlyCollection<LogEvent> events)
        {
            if(events is null) { return; }
            SafeWriteLine(Output, Title.Invoke(name, ts));
            foreach (var @event in events)
            {
                var line = Utility.FormatEvent(@event);
                SafeWriteLine(Output, line);
            }
        }

        public Task ExecuteAsync(IReadOnlyList<Exception> errors, IReadOnlyCollection<LogEvent> events)
        {
            try
            {
                var ts = DateTimeOffset.Now;
                WriteError(ts, errors);
                WriteLog(ts, events);
            }
            catch (Exception ex)
            {
                WriteError(ex);
            }
            
            return Task.CompletedTask;
        }
    }
}
