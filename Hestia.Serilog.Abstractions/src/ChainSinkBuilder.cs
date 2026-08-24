using System;

namespace Hestia.Serilog
{
    public sealed class ChainSinkBuilder
    {
        private ChainSink head = null;



        public ChainSinkBuilder With(Func<ChainSink, ChainSink> factory)
        {
            var current = factory?.Invoke(head);
            if(current is not null) { head = current; }
            return this;
        }

        public ChainSink Build()
        {
            return head;
        }
    }
}
