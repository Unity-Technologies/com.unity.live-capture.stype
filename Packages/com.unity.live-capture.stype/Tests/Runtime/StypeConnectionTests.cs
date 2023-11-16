using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.LiveCapture.Stype.Tests.Runtime
{
    class StypeConnectionTests
    {
        [Test]
        public void Instantiate()
        {
            string hostName = "127.0.0.1";
            int port = 6011;
            bool autoStartOnPlay = true;

            var connection = new StypeConnection();
            Assert.That(connection.HostName, Is.EqualTo(hostName));
            Assert.That(connection.Port, Is.EqualTo(port));
            Assert.That(connection.AutoStartOnPlay, Is.EqualTo(autoStartOnPlay));
            Assert.That(connection.TimecodeSource, Is.Null);
            Assert.That(connection.GetName(), Is.Not.Null);
            Assert.That(connection.GetClient(), Is.Not.Null);

            hostName = "192.168.10.1";
            port = 8011;
            autoStartOnPlay = false;

            connection.HostName = hostName;
            connection.Port = port;
            connection.AutoStartOnPlay = autoStartOnPlay;

            Assert.That(connection.HostName, Is.EqualTo(hostName));
            Assert.That(connection.Port, Is.EqualTo(port));
            Assert.That(connection.AutoStartOnPlay, Is.EqualTo(autoStartOnPlay));
        }

        [UnityTest]
        public IEnumerator SetEnabled()
        {
            var connection = new StypeConnection();
            Assert.That(connection.IsEnabled(), Is.False);
            Assert.That(connection.IsConnecting(), Is.False);
            Assert.That(connection.IsConnected(), Is.False);
            Assert.That(connection.IsConnectedOrConnecting(), Is.False);

            connection.SetEnabled(true);
            Assert.That(connection.IsEnabled(), Is.True);
            Assert.That(connection.IsConnecting(), Is.True);
            Assert.That(connection.IsConnected(), Is.False);
            Assert.That(connection.IsConnectedOrConnecting(), Is.True);

            yield return 0;

            connection.SetEnabled(false);
            Assert.That(connection.IsEnabled(), Is.False);
            Assert.That(connection.IsConnecting(), Is.False);
            Assert.That(connection.IsConnected(), Is.False);
            Assert.That(connection.IsConnectedOrConnecting(), Is.False);

            yield return 0;
        }
    }
}
