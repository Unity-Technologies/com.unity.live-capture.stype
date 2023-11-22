using System;
using UnityEngine;
using Stype;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Unity.LiveCapture.Stype
{

    internal class StypeClient
    {
        const int k_ReceiveTimeout = 1000;
        Thread m_Thread;
        int m_Handle = 0;
        bool m_IsAlive = false;
        IPAddress m_IPAddress;
        int m_Port;
        ITimecodeSource m_TimecodeSource = null;

        public string HostName
        {
            get => m_IPAddress.ToString();
            set
            {
                if (!string.IsNullOrEmpty(value) && m_IPAddress.ToString() != value)
                {
                    m_IPAddress = IPAddress.Parse(value);
                    IsConnected = false;
                }
            }
        }

        public int Port
        {
            get => m_Port;
            set
            {
                if (m_Port != value)
                {
                    m_Port = value;
                    IsConnected = false;
                }
            }
        }

        public ITimecodeSource TimecodeSource
        {
            get => m_TimecodeSource;
            set => m_TimecodeSource = value;
        }

        public StypeClient(string hostName = "127.0.0.1", int port = 6011)
        {
            m_IPAddress = IPAddress.Parse(hostName);
            m_Port = port;
        }

        public bool IsConnected { get; private set; }
        public bool IsConnecting => m_Thread != null && !IsConnected;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<StypeFrame> OnFrameDataReceived;

        internal void Connect()
        {
            if (m_Thread == null)
            {
                WorkerClass wc = new WorkerClass(this);
                if (m_Handle == 0)
                {
                    m_Handle = StypeAPI.Connect(m_IPAddress, m_Port, k_ReceiveTimeout);
                }

                m_Thread = new Thread(new ThreadStart(wc.Worker));
                m_IsAlive = true;
                m_Thread.Start();
            }
        }

        internal void Disconnect()
        {
            m_IsAlive = false;
            if (m_Thread != null)
            {
                m_Thread.Join(50);
                m_Thread = null;
            }

            try
            {
                StypeAPI.Disconnect(m_Handle);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("This connection is already disconnected.");
            }
            m_Handle = 0;
        }

        static void FromByteData(StypeClient client, ref StypeFrame frame)
        {
            // Override timecode
            if (client.TimecodeSource != null)
            {
                var source = client.TimecodeSource;
                var timecode = source.CurrentTime.HasValue ? source.CurrentTime.Value.ToTimecode() : default;
                frame.SetTimecode(timecode);
            }

            try
            {
                client.OnFrameDataReceived?.Invoke(frame);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        class WorkerClass
        {
            private const int k_Threshold = 192;
            private StypeClient m_controller;
            private int m_LastPacketNo = 0;

            public WorkerClass(StypeClient client)
            {
                m_controller = client;
            }


            public void Worker()
            {
#pragma warning disable 168
                m_controller.IsConnected = false;
                while (m_controller != null && m_controller.m_IsAlive)
                {
                    StypeFrame frame = new();
                    try
                    {
                        if (!StypeAPI.GetData(m_controller.m_Handle, ref frame))
                        {
                            continue;
                        }
                        if (m_LastPacketNo > frame.packetNo && m_LastPacketNo - frame.packetNo <= k_Threshold)
                        {
                            Debug.LogWarning($"[stYpe] Skipping packet {frame.packetNo}. Last received: {m_LastPacketNo}");
                            continue;
                        }
                        if (!m_controller.IsConnected)
                        {
                            m_controller.IsConnected = true;
                            m_controller.OnConnected?.Invoke();
                        }
                    }
                    catch (SocketException e)
                    {
                        // Connection has been closed by user.
                        if (!m_controller.m_IsAlive)
                            break;

                        // Connection is timeout.
                        if (e.SocketErrorCode == SocketError.TimedOut)
                            continue;

                        // Connection is opened but something error.
                        if (m_controller.IsConnected)
                        {
                            Debug.LogException(e);
                            continue;
                        }
                        // Unknown error.
                        throw e;
                    }

                    lock (m_controller)
                    {
                        FromByteData(m_controller, ref frame);
                    }
                    m_LastPacketNo = frame.packetNo;
                }
#pragma warning restore 168
                if (m_controller.IsConnected)
                {
                    m_controller.IsConnected = false;
                    m_controller.OnDisconnected?.Invoke();
                }
            }
        }
    }

    static class StypeFrameExtension
    {
        public static Timecode GetTimecode(this in StypeFrame value, FrameRate frameRate)
        {
            return Timecode.FromHMSF(frameRate, value.timecode.Hours, value.timecode.Minutes, value.timecode.Seconds, value.timecode.Frames);
        }

        public static void SetTimecode(this ref StypeFrame value, Timecode timecode)
        {
            value.timecode.Frames = timecode.Frames;
            value.timecode.Seconds = timecode.Seconds;
            value.timecode.Minutes = timecode.Minutes;
            value.timecode.Hours = timecode.Hours;
        }
    }
}
