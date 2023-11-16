using System;
using System.Linq;
using Unity.LiveCapture.Networking;
using Unity.LiveCapture.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.LiveCapture.Stype.Editor
{
    [CustomEditor(typeof(StypeConnection), true)]
    class StypeConnectionEditor : ConnectionEditor
    {
        const double k_PortRefreshPeriod = 2.0;
        const double k_InterfaceRefreshPeriod = 5.0;

        static class Contents
        {
            public static readonly GUIContent interfacesLabel = new GUIContent("Interface", "Available IP addresses on this machine.");
            public static readonly GUIContent streamStatusLabel = new GUIContent("Connection Status");
            public static readonly GUIContent connectedLabel = new GUIContent("Connected to Stype API.");
            public static readonly GUIContent connectingLabel = new GUIContent("Connecting to Stype API.");
            public static readonly GUIContent disconnectedLabel = new GUIContent("Not Connected to Stype API.");
        }

        SerializedProperty m_AutoStartOnPlay;
        SerializedProperty m_HostName;
        SerializedProperty m_Port;

        StypeConnection m_Connection;
        string m_PortMessage;
        bool m_PortAvailable;
        double m_LastPortRefreshTime;
        string[] m_Addresses;
        double m_LastInterfaceRefreshTime;
        int m_hostIndex;

        /// <inheritdoc />
        protected override void OnEnable()
        {
            base.OnEnable();

            m_AutoStartOnPlay = serializedObject.FindProperty(StypeConnection.AutoStartOnPlayFieldName);
            m_HostName = serializedObject.FindProperty(StypeConnection.HostNameFieldName);
            m_Port = serializedObject.FindProperty(StypeConnection.PortFieldName);

            m_Connection = target as StypeConnection;
            m_LastPortRefreshTime = double.MinValue;
            m_LastInterfaceRefreshTime = double.MinValue;

            UpdateAddresses();

            if (m_Addresses.Length > 0)
            {
                int index = Array.IndexOf(m_Addresses, m_HostName.stringValue);
                m_hostIndex = index >= 0 ? index : 0;
                m_Connection.HostName = m_Addresses[m_hostIndex];
            }
            else
            {
                m_hostIndex = -1;
                m_Connection.HostName = null;
            }
        }

        /// <inheritdoc />
        protected override VisualElement CreateInfoGUI()
        {
            return new IMGUIContainer(DrawInfoGUI);
        }

        /// <inheritdoc />
        protected override VisualElement CreateSettingsGUI()
        {
            return new IMGUIContainer(DrawSettingsGUI);
        }

        void DrawInfoGUI()
        {
            if (m_Connection.IsEnabled())
            {
                var rect = EditorGUILayout.GetControlRect();
                rect.xMin += 20f;

                if (m_Connection.IsConnected())
                {
                    EditorGUI.LabelField(rect, Contents.connectedLabel);
                }
                else if (m_Connection.IsConnecting())
                {
                    EditorGUI.LabelField(rect, Contents.connectingLabel);
                }
                else
                {
                    EditorGUI.LabelField(rect, Contents.disconnectedLabel);
                }
            }
        }

        void DrawSettingsGUI()
        {
            serializedObject.Update();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.Space(18f);

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.PropertyField(m_AutoStartOnPlay);

                    DrawInterfaces();

                    EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

                    DrawPort();

                    EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

                    DrawStatus();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawPort()
        {
            var refreshPort = EditorApplication.timeSinceStartup - m_LastPortRefreshTime > k_PortRefreshPeriod;
            var connected = m_Connection.IsConnectedOrConnecting();

            using (new EditorGUI.DisabledGroupScope(connected))
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(m_Port);
                refreshPort |= change.changed;
            }

            // check if the selected port is valid
            if (refreshPort && !m_Connection.IsConnectedOrConnecting())
            {
                var port = m_Port.intValue;

                m_PortAvailable = NetworkUtilities.IsPortAvailable(port);

                m_LastPortRefreshTime = EditorApplication.timeSinceStartup;
            }

            // display messages explaining why the port is not valid
            if (!m_Connection.IsConnectedOrConnecting())
            {
                if (!string.IsNullOrEmpty(m_PortMessage))
                {
                    EditorGUILayout.HelpBox(m_PortMessage, MessageType.Warning);
                }

                if (!m_PortAvailable)
                {
                    EditorGUILayout.HelpBox($"Port {m_Port.intValue} appears to be in use by another program or Unity instance! Close the other program or assign a free port.", MessageType.Warning);
                }
            }
        }

        void DrawStatus()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(Contents.streamStatusLabel);
            }

            using (new EditorGUI.IndentLevelScope())
            {
                if (m_Connection.IsConnected())
                {
                    EditorGUILayout.LabelField(Contents.connectedLabel);
                }
                else if (m_Connection.IsConnecting())
                {
                    EditorGUILayout.LabelField(Contents.connectingLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(Contents.disconnectedLabel);
                }
            }
        }

        void DrawInterfaces()
        {
            if (EditorApplication.timeSinceStartup - m_LastInterfaceRefreshTime > k_InterfaceRefreshPeriod)
            {
                UpdateAddresses();
            }

            using (new EditorGUI.DisabledGroupScope(m_Connection.IsConnectedOrConnecting()))
            {
                var newIndex = EditorGUILayout.Popup(Contents.interfacesLabel, m_hostIndex, m_Addresses);
                if (m_Addresses.Length == 0)
                {
                    EditorGUILayout.HelpBox("Unable to get local host IP address.", MessageType.Error);
                }
                if (newIndex != m_hostIndex || (m_hostIndex == -1 && m_Addresses.Length > 0))
                {
                    if (m_Addresses.Length > 0)
                    {
                        m_hostIndex = newIndex >= 0 ? newIndex : 0;
                        m_Connection.HostName = m_Addresses[m_hostIndex];
                    }
                    else
                    {
                        m_hostIndex = -1;
                        m_Connection.HostName = null;
                    }
                }
            }
        }

        void UpdateAddresses()
        {
            m_Addresses = NetworkUtilities.GetIPAddresses(false).Select(ip => ip.ToString()).ToArray();
            m_LastInterfaceRefreshTime = EditorApplication.timeSinceStartup;
        }
    }
}
