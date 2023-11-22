#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Unity.LiveCapture.Stype
{
    /// <summary>
    /// The client used to communicate with the stYpe Server.
    /// </summary>
    [CreateConnectionMenuItem("stYpe Connection")]
    class StypeConnection : Connection
    {
        internal const string HostNameFieldName = nameof(m_HostName);
        internal const string PortFieldName = nameof(m_Port);
        internal const string AutoStartOnPlayFieldName = nameof(m_AutoStartOnPlay);

        const string k_Name = "stYpe Connection";
        const string k_DefaultServer = "127.0.0.1";
        const int k_DefaultPort = 6011;

#if UNITY_EDITOR
#pragma warning disable 414
        [SerializeField, HideInInspector]
        bool m_SettingsExpanded = false;
#pragma warning restore 414
#endif
        [SerializeField, Tooltip("The hostname or IP address of the stYpe Server.")]
        string m_HostName = k_DefaultServer;
        [SerializeField, Tooltip("The port on which the client will connect to the host.")]
        int m_Port = k_DefaultPort;
        [SerializeField, Tooltip("Start the client automatically after entering play mode.")]
        bool m_AutoStartOnPlay = true;

        readonly StypeClient m_Client = new StypeClient();

        /// <summary>
        /// The hostname or IP address of the stYpe Server.
        /// </summary>
        public string HostName
        {
            get => m_HostName;
            set
            {
                if (m_HostName != value)
                {
                    m_HostName = value;
                    m_Client.HostName = value;
                    OnChanged(true);
                }
            }
        }

        /// <summary>
        /// The port on which the client will connect to the host.
        /// </summary>
        public int Port
        {
            get => m_Port;
            set
            {
                if (m_Port != value)
                {
                    m_Port = value;
                    m_Client.Port = value;
                    OnChanged(true);
                }
            }
        }

        /// <summary>
        /// Start the client automatically after entering play mode.
        /// </summary>
        public bool AutoStartOnPlay
        {
            get => m_AutoStartOnPlay;
            set
            {
                if (m_AutoStartOnPlay != value)
                {
                    m_AutoStartOnPlay = value;
                    OnChanged(true);
                }
            }
        }

        /// <summary>
        /// The timecode source used to jam the star tracker timecode.
        /// </summary>
        public ITimecodeSource TimecodeSource
        {
            get => m_Client.TimecodeSource;
            set => m_Client.TimecodeSource = value;
        }

        /// <inheritdoc/>
        protected override void OnEnable()
        {
            base.OnEnable();

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
#endif
        }

        /// <inheritdoc/>
        protected override void OnDisable()
        {
            base.OnDisable();

            StopServer();

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        void PlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                {
                    if (m_AutoStartOnPlay)
                    {
                        StartServer();
                    }
                    break;
                }
            }
        }

#endif

        /// <inheritdoc />
        public override string GetName() => k_Name;

        /// <inheritdoc />
        public override bool IsEnabled()
        {
            return IsConnecting() || IsConnected();
        }

        /// <inheritdoc />
        public override void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                StartServer();
            }
            else
            {
                StopServer();
            }
        }

        public bool IsConnected()
        {
            return m_Client.IsConnected;
        }

        public bool IsConnecting()
        {
            return m_Client.IsConnecting;
        }

        public bool IsConnectedOrConnecting()
        {
            return IsConnected() || IsConnecting();
        }

        public void StartServer()
        {
            m_Client.HostName = m_HostName;
            m_Client.Port = m_Port;
            m_Client.Connect();

            OnChanged(false);
        }

        public void StopServer()
        {
            m_Client.Disconnect();

            OnChanged(false);
        }

        internal StypeClient GetClient()
        {
            return m_Client;
        }
    }
}

