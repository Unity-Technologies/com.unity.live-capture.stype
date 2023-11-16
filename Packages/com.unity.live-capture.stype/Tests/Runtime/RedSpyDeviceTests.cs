using NUnit.Framework;
using Stype;
using UnityEngine;

namespace Unity.LiveCapture.Stype.Tests.Runtime
{
    class RedSpyDeviceTests
    {
        [Test]
        public void Instantiate()
        {
            var obj = new GameObject();
            var component = obj.AddComponent<RedSpyDevice>();

            Assert.That(component.Camera, Is.Null);
            Assert.That(component.IsReady, Is.False);
            Assert.That(component.IsRecording, Is.False);

            UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    class StypeDataExtensionsTests
    {
        [Test]
        public void LerpUnclamped()
        {
            FrameRate frameRate = StandardFrameRate.FPS_30_00;

            StypeData a = new StypeData()
            {
                timecode = new StypeData.Timecode() { hours = 0, minutes = 0, seconds = 0, frames = 0, subframes = 0, subframeResolution = 0 }
            };
            StypeData b = new StypeData()
            {
                timecode = new StypeData.Timecode() { hours = 0, minutes = 0, seconds = 0, frames = 1, subframes = 0, subframeResolution = 0 }
            };
            Assert.That(a, Is.Not.EqualTo(b));
            float t = 0.5f;
            StypeData result = StypeDataExtension.LerpUnclamped(a, b, t, frameRate);
            Timecode timecode = result.GetTimecode(frameRate);
            Assert.That(timecode.Hours, Is.EqualTo(0));
            Assert.That(timecode.Minutes, Is.EqualTo(0));
            Assert.That(timecode.Seconds, Is.EqualTo(0));
            Assert.That(timecode.Frames, Is.EqualTo(0));
            Assert.That(timecode.Subframe.Value, Is.EqualTo(Subframe.DefaultResolution * t));
            Assert.That(timecode.Subframe.Resolution, Is.EqualTo(Subframe.DefaultResolution));
        }

        [Test]
        public void TimecodeLerpUnclamped()
        {
            FrameRate frameRate = StandardFrameRate.FPS_30_00;
            Timecode a = Timecode.FromHMSF(frameRate, 0, 0, 0, 0);
            Timecode b = Timecode.FromHMSF(frameRate, 0, 0, 0, 1);
            Assert.That(a, Is.Not.EqualTo(b));

            float t = 0.5f;
            Timecode result = StypeDataExtension.LerpUnclamped(a, b, t, frameRate);
            Assert.That(result.Hours, Is.EqualTo(0));
            Assert.That(result.Minutes, Is.EqualTo(0));
            Assert.That(result.Seconds, Is.EqualTo(0));
            Assert.That(result.Frames, Is.EqualTo(0));
            Assert.That(result.Subframe.Value, Is.EqualTo(Subframe.DefaultResolution * t));
            Assert.That(result.Subframe.Resolution, Is.EqualTo(Subframe.DefaultResolution));
            Assert.That(result, Is.GreaterThan(a));
            Assert.That(result, Is.LessThan(b));
        }
    }
}
