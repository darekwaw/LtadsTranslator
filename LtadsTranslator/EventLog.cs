using System;

namespace LtadsTranslator
{
    internal class EventLog
    {
        public DateTime EventTime { get; internal set; }
        public int EventType { get; internal set; }
        public string ModuleName { get; internal set; }
        public string ItemName { get; internal set; }
    }
}