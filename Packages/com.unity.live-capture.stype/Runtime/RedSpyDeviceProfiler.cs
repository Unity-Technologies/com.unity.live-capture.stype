#if PROFILING_CORE_1_0_OR_NEWER
using System.Diagnostics;
using Unity.Profiling;

namespace Unity.LiveCapture.Stype
{
    class LiveCaptureProfiler
    {
        public static readonly ProfilerCategory LiveCaptureCategory = ProfilerCategory.Scripts;

        public enum Counter : ushort
        {
            PacketDeltaTime,
            SyncUpdateDeltaTime
        }

        interface IProfiler
        {
            void BeginSample();
            void EndSample();
        }

        class PacketDeltaTime : IProfiler
        {
            readonly Stopwatch stopWatch = new Stopwatch();

            readonly ProfilerCounterValue<double> Counter =
                new ProfilerCounterValue<double>(LiveCaptureCategory, "RedSpy Packet Deltatime", ProfilerMarkerDataUnit.TimeNanoseconds);

            public void BeginSample()
            {
                if (stopWatch.IsRunning)
                    stopWatch.Stop();
                stopWatch.Restart();
            }

            public void EndSample()
            {
                if (!stopWatch.IsRunning)
                    return;
                stopWatch.Stop();
                System.TimeSpan ts = stopWatch.Elapsed;

                // Set nanoseconds.
                Counter.Value = ts.TotalMilliseconds * 1000 * 1000;
            }
        }

        class SyncUpdateDeltaTime : IProfiler
        {
            readonly Stopwatch stopWatch = new Stopwatch();

            readonly ProfilerCounterValue<double> Counter =
                new ProfilerCounterValue<double>(LiveCaptureCategory, "SyncUpdate Deltatime", ProfilerMarkerDataUnit.TimeNanoseconds);

            public void BeginSample()
            {
                if (stopWatch.IsRunning)
                    stopWatch.Stop();
                stopWatch.Restart();
            }

            public void EndSample()
            {
                if (!stopWatch.IsRunning)
                    return;
                stopWatch.Stop();
                System.TimeSpan ts = stopWatch.Elapsed;

                // Set nanoseconds.
                Counter.Value = ts.TotalMilliseconds * 1000 * 1000;
            }
        }

        static PacketDeltaTime m_packetDeltaTime = new PacketDeltaTime();
        static SyncUpdateDeltaTime m_syncUpdateDeltaTime = new SyncUpdateDeltaTime();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void InitializeLiveCaptureProfiler()
        {
            // To display profiler Counters in Unity Profiling Core API, 
            // it needs to set samples at least once.
            m_packetDeltaTime.BeginSample();
            m_packetDeltaTime.EndSample();
            m_syncUpdateDeltaTime.BeginSample();
            m_syncUpdateDeltaTime.EndSample();
        }
#endif

        public static void BeginSample(Counter counter)
        {
            switch(counter)
            {
                case Counter.PacketDeltaTime:
                    m_packetDeltaTime.BeginSample();
                    return;
                case Counter.SyncUpdateDeltaTime:
                    m_syncUpdateDeltaTime.BeginSample();
                    return;
            }
        }

        public static void EndSample(Counter counter)
        {
            switch (counter)
            {
                case Counter.PacketDeltaTime:
                    m_packetDeltaTime.EndSample();
                    return;
                case Counter.SyncUpdateDeltaTime:
                    m_syncUpdateDeltaTime.EndSample();
                    return;
            }
        }

    }
}
#endif
