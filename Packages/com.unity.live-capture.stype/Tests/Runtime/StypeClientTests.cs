using NUnit.Framework;
using System.Collections;
using UnityEngine.TestTools;

namespace Unity.LiveCapture.Stype.Tests.Runtime
{
    class StypeClientTests
    {
        [UnityTest]
        [Timeout(5000)]
        public IEnumerator ConnectFailed()
        {
            var client = new StypeClient();
            Assert.That(client.IsConnected, Is.False);
            Assert.That(client.IsConnecting, Is.False);

            bool calledOnConnected = false;
            client.OnConnected += () =>
            {
                calledOnConnected = true;
            };
            client.Connect();

            // UDP haven't connected, so this line will finish by timeout.
            yield return new WaitUntilWithTimeout(() => calledOnConnected, 1.0f);
            Assert.That(calledOnConnected, Is.False);

            Assert.That(client.IsConnected, Is.False);
            Assert.That(client.IsConnecting, Is.True);

            bool calledOnDisconnected = false;
            client.OnDisconnected += () =>
            {
                calledOnDisconnected = true;
            };
            client.Disconnect();

            // UDP haven't connected, so this line will finish by timeout.
            yield return new WaitUntilWithTimeout(() => calledOnDisconnected, 1.0f);
            Assert.That(calledOnDisconnected, Is.False);

            Assert.That(client.IsConnected, Is.False);
            Assert.That(client.IsConnecting, Is.False);
        }
    }
}

