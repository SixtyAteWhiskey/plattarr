using NLog;
using NLog.Common;
using NLog.Targets.Syslog;
using NzbDrone.Common.Instrumentation;

namespace NzbDrone.Core.Instrumentation
{
    public class CleansingSyslogTarget : SyslogTarget
    {
        protected override void Write(AsyncLogEventInfo asyncLogEvent)
        {
            var source = asyncLogEvent.LogEvent;
            var message = source.FormattedMessage;

            if (source.Exception != null)
            {
                message += " " + source.Exception;
            }

            // The Syslog target formats LogEventInfo itself, bypassing the
            // cleansing layouts used by files and the console.
            var clean = new LogEventInfo(source.Level, source.LoggerName, CleanseLogMessage.Cleanse(message))
            {
                TimeStamp = source.TimeStamp
            };

            base.Write(new AsyncLogEventInfo(clean, asyncLogEvent.Continuation));
        }
    }
}
