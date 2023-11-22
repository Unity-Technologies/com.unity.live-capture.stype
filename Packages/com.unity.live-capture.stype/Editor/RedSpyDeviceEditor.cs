using Stype;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.LiveCapture.Cameras;
using Unity.LiveCapture.Cameras.Editor;

using LensDistortion = Unity.LiveCapture.Stype.Rendering.LensDistortionStype;

namespace Unity.LiveCapture.Stype.Editor
{
    [CustomEditor(typeof(RedSpyDevice))]
    internal class RedSpyDeviceEditor : CameraTrackingDeviceEditor
    {
        static readonly string IconPath = "Packages/com.unity.live-capture.stype/Editor/Icons";

        static class Contents
        {
            public static GUIContent TimecodeSourceLabel = EditorGUIUtility.TrTextContent("Timecode Source", "Timecode Source to use to jam the RedSpy.");
            public static GUIContent SecondaryCameras = EditorGUIUtility.TrTextContent("Secondary Cameras", "Array of cameras which are copied properties from a primary camera.");
            public static GUIContent EyeDistanceOffsetRatio = EditorGUIUtility.TrTextContent("Eye Distance Offset Ratio", "Ratio of the offset of an eye point from a camera sensor.");
            public static GUIContent RawDataLabel = EditorGUIUtility.TrTextContent("Raw Data", "Raw Data from RedSpy.");
            public static GUIContent DOFModeHelpboxLabel = EditorGUIUtility.TrTextContent("DOF mode is disabled on your RedSpy device. Please set enable DOF mode on your device.");
            public static GUIContent PhysicalCameraHelpboxLabel = EditorGUIUtility.TrTextContentWithIcon("Physical camera is disabled on your camera component. Please enable physical camera property.", "console.warnicon");

            public static GUIContent CreateRequiredComponents(Transform root)
            {
                return EditorGUIUtility.TrTextContentWithIcon(
                    string.Format("The selected actor {0} requires extra components to work properly.", root.gameObject.name),
                    "console.warnicon");
            }

            public static string TimecodeLabel = "Timecode";
            public static GUIContent TranslationLabel = EditorGUIUtility.TrTextContent("Translation", "The translation of the camera.");
            public static GUIContent RotationLabel = EditorGUIUtility.TrTextContent("Rotation", "The rotation of the camera.");
            public static GUIContent FocusLabel = EditorGUIUtility.TrTextContent("Focus", "The encoder focus value. If you set enable \"DOF mode\" on stYpe device, this value returns as a focus distance.  If don't use DOF mode, it returns 0-close, 1-far (infinite)");
            public static GUIContent ZoomLabel = EditorGUIUtility.TrTextContent("Zoom", "The raw encoder zoom value. 0-wide, 1-tele.");
            public static GUIContent K1Label = EditorGUIUtility.TrTextContent("K1", "The radial distortion coefficient. First radial distortion harmonic (mm-2).");
            public static GUIContent K2Label = EditorGUIUtility.TrTextContent("K2", "The radial distortion coefficient. Second radial distortion harmonic (mm-4).");
            public static GUIContent CenterLabel = EditorGUIUtility.TrTextContent("Center", "Center zoom shift (in mm).");
            public static GUIContent FieldOfViewLabel = EditorGUIUtility.TrTextContent("Field Of View", "Horizontal field of view(degrees).");
            public static GUIContent SensorSizeLabel = EditorGUIUtility.TrTextContent("Sensor Size", "The sensor size of the camera.");
        }

        class Styles
        {
            public static GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                margin = new RectOffset(0, 0, 6, 6)
            };
        }

        static readonly Type[] s_RequiredComponents = new[]
        {
            typeof(FieldOfViewToFocalLength),
            typeof(DepthOfField),
            typeof(LensDistortion)
        };

        static readonly Type[] s_RequiredComponentsSecondaryCamera = new[]
{
            typeof(FieldOfViewToFocalLength),
            typeof(DepthOfField),
            typeof(LensDistortion),
            typeof(SecondaryCamera)
        };

        SerializedProperty m_SecondaryCameras;
        SerializedProperty m_EyeDistanceOffsetRatio;
        SerializedProperty m_TimecodeSource;
        SerializedProperty m_FrameRate;
        Texture m_TitleIcon;
        RedSpyDevice m_Device;

        bool m_FoldoutStypeData = false;
        bool m_FoldoutStypeFrame = false;

        Dictionary<Transform, List<(Transform target, Type type)>> m_RequiredComponents =
            new Dictionary<Transform, List<(Transform target, Type type)>>();

        Dictionary<Transform, GUIContent> m_GUIContentRequiredComponents =
            new Dictionary<Transform, GUIContent>();


        protected override void OnEnable()
        {
            base.OnEnable();

            m_TitleIcon = (Texture)AssetDatabase.LoadAssetAtPath($"{IconPath}/stype-logo.png", typeof(Texture));
            m_SecondaryCameras = serializedObject.FindProperty(RedSpyDevice.SecondaryCamerasFieldName);
            m_EyeDistanceOffsetRatio = serializedObject.FindProperty(RedSpyDevice.EyeDistanceOffsetRatioFieldName);
            m_TimecodeSource = serializedObject.FindProperty(RedSpyDevice.TimecodeSourceFieldName);
            m_FrameRate = serializedObject.FindProperty(RedSpyDevice.FrameRateFieldName);
            m_Device = serializedObject.targetObject as RedSpyDevice;

            EditorApplication.update += Update;
            EditorApplication.hierarchyChanged += HierarchyChanged;
        }

        protected void OnDisable()
        {
            EditorApplication.update -= Update;
            EditorApplication.hierarchyChanged -= HierarchyChanged;
        }

        protected void Update()
        {
            if (m_Device != null && m_Device.IsReady())
            {
                EditorUtility.SetDirty(target);
            }
        }

        protected void HierarchyChanged()
        {
            UpdateRequiredComponents();
        }

        protected void UpdateRequiredComponents()
        {
            m_RequiredComponents.Clear();
            m_GUIContentRequiredComponents.Clear();

            for (int indexCamera = 0; indexCamera < m_Device.SecondaryCameras.Length; indexCamera++)
            {
                var camera = m_Device.SecondaryCameras[indexCamera];
                if (camera == null)
                    continue;

                var transform = camera.transform;

                if (!m_RequiredComponents.ContainsKey(transform))
                    m_RequiredComponents.Add(transform, new List<(Transform target, Type type)>());
                if (!m_GUIContentRequiredComponents.ContainsKey(transform))
                    m_GUIContentRequiredComponents.Add(transform, Contents.CreateRequiredComponents(transform));

                var requiredComponents = m_RequiredComponents[transform];
                var requiredRootComponents = s_RequiredComponentsSecondaryCamera;
                if (requiredRootComponents != null)
                {
                    for (int indexType = 0; indexType < requiredRootComponents.Length; indexType++)
                    {
                        var type = requiredRootComponents[indexType];
                        if (!typeof(Component).IsAssignableFrom(type))
                        {
                            continue;
                        }

                        if (!transform.TryGetComponent(type, out _))
                        {
                            requiredComponents.Add((transform, type));
                        }
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DoTopButtons();

            EditorGUILayout.Separator();

            DrawDefaultCameraTrackerInspector();

            var root = m_Device?.Camera != null ? m_Device.Camera.transform : null;
            DrawDefaultLiveStreamInspector(root);

            serializedObject.Update();

            if (m_Device != null && !m_Device.Camera.usePhysicalProperties)
            {
                LiveCaptureGUI.DrawFixMeBox(Contents.PhysicalCameraHelpboxLabel, () =>
                {
                    m_Device.Camera.usePhysicalProperties = true;
                });
            }

            DrawSecondaryCamerasInspector();

            string textTimecode = m_Device != null ? m_Device.Timecode.ToString() : "Timecode source is not found.";
            EditorGUILayout.PropertyField(m_EyeDistanceOffsetRatio, Contents.EyeDistanceOffsetRatio, true);
            EditorGUILayout.PropertyField(m_TimecodeSource, Contents.TimecodeSourceLabel, true);
            EditorGUILayout.LabelField(Contents.TimecodeLabel, textTimecode);
            EditorGUILayout.PropertyField(m_FrameRate);

            if (m_Device != null && m_Device.IsReady() && !m_Device.Current.isDOFMode)
            {
                EditorGUILayout.HelpBox(Contents.DOFModeHelpboxLabel.text, MessageType.Warning);
            }

            StypeData data = m_Device.Current;
            StypeFrame frame = m_Device.RawData;

            StypeDataField(ref data);
            EditorGUI.BeginDisabledGroup(true);
            {
                StypeFrameField(ref frame);
            }
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }

        void StypeDataField(ref StypeData data)
        {
            m_FoldoutStypeData = EditorGUILayout.Foldout(m_FoldoutStypeData, "Current Data");
            if (m_FoldoutStypeData)
            {
                EditorGUILayout.Vector3Field(Contents.TranslationLabel, data.translation);
                EditorGUILayout.Vector3Field(Contents.RotationLabel, data.rotation.eulerAngles);
                EditorGUILayout.FloatField(Contents.FieldOfViewLabel, data.fieldOfView);
                EditorGUILayout.FloatField(Contents.FocusLabel, data.focus);
                EditorGUILayout.FloatField(Contents.ZoomLabel, data.zoom);
                EditorGUILayout.FloatField(Contents.K1Label, data.k1);
                EditorGUILayout.FloatField(Contents.K2Label, data.k2);
                EditorGUILayout.Vector2Field(Contents.CenterLabel, data.GetLensShift());
                EditorGUILayout.Vector2Field(Contents.SensorSizeLabel, data.GetSensorSize());
            }
        }

        void StypeFrameField(ref StypeFrame frame)
        {
            m_FoldoutStypeFrame = EditorGUILayout.Foldout(m_FoldoutStypeFrame, "Raw Data");
            if (m_FoldoutStypeFrame)
            {
                EditorGUILayout.IntField("Header", frame.header);
                EditorGUILayout.IntField("Packet No", frame.packetNo);
                EditorGUILayout.FloatField("Hours", frame.timecode.Hours);
                EditorGUILayout.FloatField("Minutes", frame.timecode.Minutes);
                EditorGUILayout.FloatField("Seconds", frame.timecode.Seconds);
                EditorGUILayout.FloatField("Frames", frame.timecode.Frames);
                EditorGUILayout.FloatField("X", frame.x);
                EditorGUILayout.FloatField("Y", frame.y);
                EditorGUILayout.FloatField("Z", frame.z);
                EditorGUILayout.FloatField("Pan", frame.pan);
                EditorGUILayout.FloatField("Tilt", frame.tilt);
                EditorGUILayout.FloatField("Roll", frame.roll);
                EditorGUILayout.FloatField("Fov X", frame.fovX);
                EditorGUILayout.FloatField("Aspect Ratio", frame.aspectRatio);
                EditorGUILayout.FloatField("Focus", frame.focus);
                EditorGUILayout.FloatField("Zoom", frame.zoom);
                EditorGUILayout.FloatField("K1", frame.focus);
                EditorGUILayout.FloatField("K2", frame.zoom);
                EditorGUILayout.FloatField("Center X", frame.centerX);
                EditorGUILayout.FloatField("Center Y", frame.centerY);
                EditorGUILayout.FloatField("Projection Area Width", frame.projectionAreaWidth);
                EditorGUILayout.IntField("Checksum", frame.checksum);
            }
        }

        protected override IEnumerable<Type> GetRequiredComponents()
        {
            return s_RequiredComponents;
        }

        void DrawSecondaryCamerasInspector()
        {
            EditorGUILayout.PropertyField(m_SecondaryCameras, Contents.SecondaryCameras, true);

            for (int index = 0; index < m_Device.SecondaryCameras.Length; index++)
            {
                var camera = m_Device.SecondaryCameras[index];
                if (camera == null)
                    continue;
                var transform = camera.transform;
                DrawRequiredComponentsSecondaryCamera(transform);
            }
        }

        void DrawRequiredComponentsSecondaryCamera(Transform root)
        {
            if (!m_RequiredComponents.ContainsKey(root))
            {
                UpdateRequiredComponents();
            }

            var requiredComponents = m_RequiredComponents[root];
            if (requiredComponents.Count > 0)
            {
                LiveCaptureGUI.DrawFixMeBox(m_GUIContentRequiredComponents[root], () =>
                {
                    AddRequiredComponents(requiredComponents.ToArray());
                    SetUpSecondaryCamera(root);
                });
            }
        }

        void SetUpSecondaryCamera(Transform root)
        {
            var camera = root.GetComponent<Camera>();
            if (camera == null)
                return;
            var secondaryCamera = camera.GetComponent<SecondaryCamera>();
            if (secondaryCamera == null)
                return;
            if (secondaryCamera.PrimaryCamera == null)
                secondaryCamera.PrimaryCamera = m_Device.Camera;
        }

        void DoTopButtons()
        {
            GUILayout.Space(15);
            GUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(m_TitleIcon, GUILayout.MinWidth(1), GUILayout.MaxHeight(48));
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Camera tracking: {StypeInfo.RedSpyName} by {StypeInfo.CompanyName}", Styles.titleStyle);
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
        }
    }
}
