using UnityEngine;
using UnityEditor;
using Unity.LiveCapture.Stype.Rendering;

#if HDRP_14_0_OR_NEWER
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Unity.LiveCapture.Stype.Editor
{
    using Editor = UnityEditor.Editor;

    [CustomEditor(typeof(LensDistortionStype))]
    class LensDistortionStypeEditor : Editor
    {
        static class Contents
        {
            public static readonly GUIContent UseDistortionScale = new GUIContent("Use Over Scan Scale", "Whether to use the over scan scale.");
            public static readonly GUIContent OverScanScale = new GUIContent("Over Scan Scale", "The scale of the distortion effect.");
            public static readonly GUIContent RadialCoefficients = new GUIContent("Radial Coefficients", "The radial distortion coefficients.");
            public static readonly GUIContent CenterPoint = new GUIContent("Center Point", "The center point of distortion.");
            public static readonly GUIContent FixInjectionPoint = EditorGUIUtility.TrTextContentWithIcon(
                $"Requires adding {typeof(LensDistortionVolumeComponent).Name} as a CustomPostProcess with 'After Post Process' order in HDRP Global Settings.",
                "console.warnicon");
            public static readonly GUIContent FixPostProcessingBufferFormat = EditorGUIUtility.TrTextContentWithIcon(
                "Requires R16G16B16A16 buffer format for HDRP post processing.",
                "console.warnicon");
        }

#if HDRP_14_0_OR_NEWER
        static bool ValidatePostProcessingBufferFormat()
        {
            HDRenderPipelineAsset asset = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset;
            RenderPipelineSettings renderPipelineSettings = asset.currentPlatformRenderPipelineSettings;
            GlobalPostProcessSettings postProcessSettings = renderPipelineSettings.postProcessSettings;
            return postProcessSettings.bufferFormat == PostProcessBufferFormat.R16G16B16A16;
        }

        static void FixPostProcessingBufferFormat()
        {
            HDRenderPipelineAsset asset = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset;

            var serializedObject = new SerializedObject(asset);
            var renderPipelineSettingsProp = serializedObject.FindProperty("m_RenderPipelineSettings");
            var postProcessSettingsProp = renderPipelineSettingsProp.FindPropertyRelative("postProcessSettings");
            var bufferFormatProp = postProcessSettingsProp.FindPropertyRelative("bufferFormat");

            serializedObject.Update();

            bufferFormatProp.intValue = (int)PostProcessBufferFormat.R16G16B16A16;

            serializedObject.ApplyModifiedProperties();
        }
#endif

        SerializedProperty m_RadialCoefficients;
        SerializedProperty m_CenterPoint;

        public void OnEnable()
        {
            m_RadialCoefficients = serializedObject.FindProperty(LensDistortionStype.RadialCoefficientsFieldName);
            m_CenterPoint = serializedObject.FindProperty(LensDistortionStype.CenterPointFieldName);
        }

        public override void OnInspectorGUI()
        {
#if HDRP_14_0_OR_NEWER
            if (!HDRPEditorUtilities.ContainsPostEffect<LensDistortionVolumeComponent>(CustomPostProcessInjectionPoint.AfterPostProcess))
            {
                LiveCaptureGUI.DrawFixMeBox(Contents.FixInjectionPoint, () =>
                {
                    HDRPEditorUtilities.AddPostEffect<LensDistortionVolumeComponent>(CustomPostProcessInjectionPoint.AfterPostProcess);
                });
            }
            if (!ValidatePostProcessingBufferFormat())
            {
                LiveCaptureGUI.DrawFixMeBox(Contents.FixPostProcessingBufferFormat, () =>
                {
                    FixPostProcessingBufferFormat();
                });
            }
#endif
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_RadialCoefficients, Contents.RadialCoefficients);
            EditorGUILayout.PropertyField(m_CenterPoint, Contents.CenterPoint);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
