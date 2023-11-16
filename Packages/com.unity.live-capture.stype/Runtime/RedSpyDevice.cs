using Stype;
using System.Collections.Concurrent;
using UnityEngine;
using Unity.LiveCapture.Cameras;

using LensDistortion = Unity.LiveCapture.Stype.Rendering.LensDistortionStype;

namespace Unity.LiveCapture.Stype
{
    /// <summary>
    /// A device used to connect to a Stype Red Spy to drive a virtual camera.
    /// </summary>
    [CreateDeviceMenuItem(StypeInfo.CompanyName + "/" + StypeInfo.RedSpyName)]
    public sealed class RedSpyDevice : CameraTrackingDevice
    {
        const int k_MaxFrameQueueLength = 64;

        internal const string SecondaryCamerasFieldName = nameof(m_SecondaryCameras);
        internal const string TimecodeSourceFieldName = nameof(m_TimecodeSource);
        internal const string FrameRateFieldName = nameof(m_FrameRate);
        internal const string EyeDistanceOffsetRatioFieldName = nameof(m_EyeDistanceOffsetRatio);

        class Interpolator : IInterpolator<StypeData>
        {
            public static Interpolator Instance(in FrameRate frameRate) => new Interpolator(frameRate);

            private Interpolator(in FrameRate frameRate)
            {
                m_FrameRate = frameRate;
            }

            private FrameRate m_FrameRate;

            public StypeData Interpolate(in StypeData a, in StypeData b, float t)
            {
                return StypeDataExtension.LerpUnclamped(a, b, t, m_FrameRate);
            }
        }

        [SerializeField]
        Camera[] m_SecondaryCameras = new Camera[0];

        [SerializeField]
        TimecodeSourceRef m_TimecodeSource = new TimecodeSourceRef(null);

        [SerializeField, OnlyStandardFrameRates]
        FrameRate m_FrameRate = StandardFrameRate.FPS_24_00;

        StypeData m_Current;

        StypeFrame m_RawData;

        Timecode m_Timecode;

        [SerializeField, Range(-3.0f, 3.0f)]
        float m_EyeDistanceOffsetRatio;

        LivePropertyHandle[] m_Handles;
        StypeClient m_Client;
        ConcurrentQueue<StypeData> m_Frames = new ConcurrentQueue<StypeData>();

        /// <summary>
        /// 
        /// </summary>
        public StypeData Current => m_Current;

        /// <summary>
        /// 
        /// </summary>
        public Timecode Timecode => m_Timecode;

        internal StypeFrame RawData => m_RawData;
        internal Camera[] SecondaryCameras => m_SecondaryCameras;


        /// <inheritdoc/>
        protected override void OnDisable()
        {
            base.OnDisable();

            DeregisterClient();
        }

        /// <inheritdoc/>
        protected override ITimedDataBuffer CreateTimedDataBuffer()
        {
            return TimedDataBuffer.Create(Interpolator.Instance(m_FrameRate));
        }

        /// <inheritdoc/>
        public override bool IsReady()
        {
            return m_Client != null && m_Client.IsConnected;
        }

        /// <inheritdoc />
        protected override void UpdateDevice()
        {
            UpdateClient();

            while (m_Frames.TryDequeue(out var value))
            {
                FrameTimeWithRate? frameTime = null;
                var frameRate = m_FrameRate;
                var timecode = value.GetTimecode(frameRate);
                if (timecode != default && frameRate.IsValid)
                {
                    frameTime = new FrameTimeWithRate(frameRate, timecode.ToFrameTime(frameRate));
                }

                AddFrame(value, frameTime);

                m_Current = value;
                m_Timecode = timecode;
            }
        }

        void UpdateClient()
        {
            if (!ConnectionManager.Instance.TryGetConnection(out StypeConnection connection))
            {
                DeregisterClient();
                return;
            }

            var client = connection.GetClient();

            if (m_Client != client)
            {
                DeregisterClient();

                m_Client = client;

                if (m_Client != null)
                {
                    m_Client.OnConnected += OnClientConnected;
                    m_Client.OnDisconnected += OnClientDisconnected;
                    m_Client.OnFrameDataReceived += OnFrameDataReceivedAsync;
                }
            }
            connection.TimecodeSource = m_TimecodeSource.Resolve();
        }

        void DeregisterClient()
        {
            if (m_Client != null)
            {
                m_Client.OnConnected -= OnClientConnected;
                m_Client.OnDisconnected -= OnClientDisconnected;
                m_Client.OnFrameDataReceived -= OnFrameDataReceivedAsync;

                ClearBuffers();
            }
        }

        void OnClientConnected()
        {
            ClearBuffers();
        }

        void OnClientDisconnected()
        {
            ClearBuffers();
        }

        void ClearBuffers()
        {
            ResetSyncBuffer();

            m_Frames.Clear();
        }

        void OnFrameDataReceivedAsync(StypeFrame frame)
        {
#if PROFILING_CORE_1_0_OR_NEWER
            LiveCaptureProfiler.EndSample(LiveCaptureProfiler.Counter.PacketDeltaTime);
            LiveCaptureProfiler.BeginSample(LiveCaptureProfiler.Counter.PacketDeltaTime);
#endif
            m_RawData = frame;

            // Since this is called from another thread we must queue the frame to be added to the
            // synchronization buffer or recorded later from the main thread.
            StypeData data = (StypeData)m_RawData;
            m_Frames.Enqueue(data);

            // prevent the queue from growing unreasonably large
            while (m_Frames.Count > k_MaxFrameQueueLength && m_Frames.TryDequeue(out _))
            {
            }
        }

        /// <inheritdoc/>
        protected override void CreateLiveProperties(LiveStream stream)
        {
            m_Handles = new LivePropertyHandle[]
            {
                stream.CreateProperty<Transform, Vector3>(string.Empty, "m_LocalPosition"),
                stream.CreateProperty<Transform, Quaternion>(string.Empty, "m_LocalEuler"),
                stream.CreateProperty<Camera, float>(string.Empty, "field of view"),
                stream.CreateProperty<FieldOfViewToFocalLength, float>(string.Empty, "m_FieldOfView", (c,v) => c.FieldOfView = v),
                stream.CreateProperty<Camera, float>(string.Empty, "m_FocusDistance", (c,v) => c.focusDistance = v),
                stream.CreateProperty<Camera, Vector2>(string.Empty, "m_SensorSize", (c,v) => c.sensorSize = v),
                stream.CreateProperty<Camera, int>(string.Empty, "m_GateFit", (c,v) => c.gateFit = (Camera.GateFitMode)v),
                stream.CreateProperty<LensDistortion, Vector2>(string.Empty, "m_CenterPoint", (c,v) => c.CenterPoint = v),
                stream.CreateProperty<LensDistortion, Vector3>(string.Empty, "m_RadialCoefficients", (c,v) => c.RadialCoefficients = v),
                stream.CreateProperty<TimecodeComponent, int>(string.Empty, "m_Hours", (c,v) => c.Hours = v),
                stream.CreateProperty<TimecodeComponent, int>(string.Empty, "m_Minutes", (c,v) => c.Minutes = v),
                stream.CreateProperty<TimecodeComponent, int>(string.Empty, "m_Seconds", (c,v) => c.Seconds = v),
                stream.CreateProperty<TimecodeComponent, int>(string.Empty, "m_Frames", (c,v) => c.Frames = v),
                stream.CreateProperty<TimecodeComponent, bool>(string.Empty, "m_IsDropFrame", (c,v) => c.IsDropFrame = v),
                stream.CreateProperty<TimecodeComponent, int>(string.Empty, "m_Subframe", (c,v) => c.Subframe = v),
                stream.CreateProperty<TimecodeComponent, int>(string.Empty, "m_Resolution", (c,v) => c.Resolution = v),
                stream.CreateProperty<TimecodeComponent, int>(string.Empty, "m_RateNumerator", (c,v) => c.RateNumerator = v),
                stream.CreateProperty<TimecodeComponent, int>(string.Empty, "m_RateDenominator", (c,v) => c.RateDenominator = v),
                stream.CreateProperty<TimecodeComponent, bool>(string.Empty, "m_RateIsDropFrame", (c,v) => c.RateIsDropFrame = v)
            };
        }

        /// <inheritdoc/>
        protected override void ProcessFrame(LiveStream stream)
        {
            if (Camera == null)
                return;

#if PROFILING_CORE_1_0_OR_NEWER
            LiveCaptureProfiler.EndSample(LiveCaptureProfiler.Counter.SyncUpdateDeltaTime);
            LiveCaptureProfiler.BeginSample(LiveCaptureProfiler.Counter.SyncUpdateDeltaTime);
#endif
            var value = GetCurrentFrame<StypeData>();
            var fieldOfView = value.GetVerticalFieldOfView();
            var frameTime = CurrentFrameTime.Value.Time;
            var frameRate = CurrentFrameTime.Value.Rate;
            var timecode = Timecode.FromFrameTime(frameRate, frameTime);

            stream.SetValue(m_Handles[0], GetCameraPosition(value, Camera, m_EyeDistanceOffsetRatio));
            stream.SetValue(m_Handles[1], value.rotation);
            stream.SetValue(m_Handles[2], fieldOfView);
            stream.SetValue(m_Handles[3], fieldOfView);
            stream.SetValue(m_Handles[4], GetFocusDistance(value, Camera));
            stream.SetValue(m_Handles[5], GetSensorSize(value, Camera));
            stream.SetValue(m_Handles[6], (int)Camera.GateFitMode.None);
            stream.SetValue(m_Handles[7], new Vector2(value.centerX, value.centerY));
            stream.SetValue(m_Handles[8], new Vector3(value.k1, value.k2));
            stream.SetValue(m_Handles[9], timecode.Hours);
            stream.SetValue(m_Handles[10], timecode.Minutes);
            stream.SetValue(m_Handles[11], timecode.Seconds);
            stream.SetValue(m_Handles[12], timecode.Frames);
            stream.SetValue(m_Handles[13], timecode.IsDropFrame);
            stream.SetValue(m_Handles[14], timecode.Subframe.Value);
            stream.SetValue(m_Handles[15], timecode.Subframe.Resolution);
            stream.SetValue(m_Handles[16], frameRate.Numerator);
            stream.SetValue(m_Handles[17], frameRate.Denominator);
            stream.SetValue(m_Handles[18], frameRate.IsDropFrame);
        }

        static Vector2 GetSensorSize(in StypeData data, Camera camera)
        {
            if (Mathf.Approximately(data.sensorWidth, 0f) || Mathf.Approximately(data.aspectRatio, 0f))
            {
                return camera.sensorSize;
            }
            return data.GetSensorSize();
        }

        /// <summary>
        /// Focus distance (in meter).
        /// </summary>
        /// <param name="data"></param>
        /// <param name="camera"></param>
        /// <returns></returns>
        static float GetFocusDistance(in StypeData data, Camera camera)
        {
            return data.focus + camera.focalLength * 0.001f;
        }

        /// <summary>
        /// Camera position.
        /// Unity defines a camera position is center of sensor. Stype provides positions as a focal point of lens.
        /// This method returns sum of focal point and offset of focal length.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="camera"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        static Vector3 GetCameraPosition(in StypeData data, Camera camera, float ratio)
        {
            return data.translation + GetEyeDistanceOffset(data, camera) * ratio;
        }

        static Vector3 GetEyeDistanceOffset(in StypeData data, Camera camera)
        {
            Vector3 offset = new Vector3(0.0f, 0.0f, camera.focalLength * 0.001f);
            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetTRS(Vector3.zero, data.rotation.normalized, Vector3.one);
            return matrix * offset;
        }
    }

    internal static class StypeDataExtension
    {
        public static Timecode GetTimecode(this in StypeData data, in FrameRate frameRate)
        {
            return Timecode.FromHMSF(
                frameRate,
                data.timecode.hours,
                data.timecode.minutes,
                data.timecode.seconds,
                data.timecode.frames,
                new Subframe(data.timecode.subframes, data.timecode.subframeResolution));
        }

        public static double GetSeconds(this in StypeData data, in FrameRate frameRate)
        {
            return data.GetTimecode(frameRate).ToSeconds(frameRate);
        }

        public static void SetTimecode(this ref StypeData data, in Timecode timecode)
        {
            data.timecode.hours = (byte)timecode.Hours;
            data.timecode.minutes = (byte)timecode.Minutes;
            data.timecode.seconds = (byte)timecode.Seconds;
            data.timecode.frames = (byte)timecode.Frames;
            data.timecode.subframes = timecode.Subframe.Value;
            data.timecode.subframeResolution = timecode.Subframe.Resolution;
        }

        public static Timecode LerpUnclamped(in Timecode a, in Timecode b, float t, in FrameRate frameRate)
        {
            var seconds = Mathf.LerpUnclamped((float)a.ToSeconds(frameRate), (float)b.ToSeconds(frameRate), t);
            return Timecode.FromSeconds(frameRate, seconds);
        }

        public static StypeData LerpUnclamped(in StypeData a, in StypeData b, float t, in FrameRate frameRate)
        {
            Timecode timecode = LerpUnclamped(a.GetTimecode(frameRate), b.GetTimecode(frameRate), t, frameRate);

            return new StypeData()
            {
                translation = Vector3.LerpUnclamped(a.translation, b.translation, t),
                rotation = Quaternion.SlerpUnclamped(a.rotation, b.rotation, t),
                focus = Mathf.LerpUnclamped(a.focus, b.focus, t),
                zoom = Mathf.LerpUnclamped(a.zoom, b.zoom, t),
                k1 = Mathf.LerpUnclamped(a.k1, b.k1, t),
                k2 = Mathf.LerpUnclamped(a.k2, b.k2, t),
                centerX = Mathf.LerpUnclamped(a.centerX, b.centerX, t),
                centerY = Mathf.LerpUnclamped(a.centerY, b.centerY, t),
                fieldOfView = Mathf.LerpUnclamped(a.fieldOfView, b.fieldOfView, t),
                aspectRatio = Mathf.LerpUnclamped(a.aspectRatio, b.aspectRatio, t),
                sensorWidth = Mathf.LerpUnclamped(a.sensorWidth, b.sensorWidth, t),
                timecode = new StypeData.Timecode()
                {
                    hours = (byte)timecode.Hours,
                    minutes = (byte)timecode.Minutes,
                    seconds = (byte)timecode.Seconds,
                    frames = (byte)timecode.Frames,
                    subframes = timecode.Subframe.Value,
                    subframeResolution = timecode.Subframe.Resolution,
                }
            };
        }
    }
}
