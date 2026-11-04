using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NLog;
using NLog.Config;
using NLog.Targets.Syslog.Settings;
using NUnit.Framework;
using NzbDrone.Core.Instrumentation;

namespace NzbDrone.Core.Test.Instrumentation
{
    [TestFixture]
    [NonParallelizable]
    public class CleansingSyslogTargetFixture
    {
        [Test]
        public async Task should_cleanse_message_and_exception_before_udp_send()
        {
            using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var port = ((IPEndPoint)receiver.Client.LocalEndPoint).Port;
            var original = LogManager.Configuration;

            try
            {
                var target = new CleansingSyslogTarget { Name = "cleansingSyslogFixture" };
                target.MessageSend.Protocol = NLog.Targets.Syslog.Settings.ProtocolType.Udp;
                target.MessageSend.Udp.Server = "127.0.0.1";
                target.MessageSend.Udp.Port = port;
                target.MessageCreation.Rfc = RfcNumber.Rfc5424;

                var configuration = new LoggingConfiguration();
                configuration.AddTarget(target);
                configuration.AddRule(LogLevel.Info, LogLevel.Fatal, target, "CleansingSyslogFixture");
                LogManager.Configuration = configuration;

                LogManager.GetLogger("CleansingSyslogFixture")
                          .Error(new Exception("https://example.test/?token=exception-secret"), "https://example.test/?apikey=message-secret");
                LogManager.Flush(TimeSpan.FromSeconds(5));

                var receive = receiver.ReceiveAsync();
                var completed = await Task.WhenAny(receive, Task.Delay(TimeSpan.FromSeconds(5)));
                completed.Should().Be(receive);

                var payload = Encoding.UTF8.GetString(receive.Result.Buffer);
                payload.Should().Contain("(removed)");
                payload.Should().NotContain("message-secret");
                payload.Should().NotContain("exception-secret");
            }
            finally
            {
                LogManager.Configuration = original;
            }
        }
    }
}
