#if HDRP_14_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;
using UnityEngine.Assertions;

namespace Unity.LiveCapture.Stype.Rendering
{
    /// <summary>
    /// 
    /// </summary>
    public struct LensDistortionStypeData
    {
        /// <summary>
        /// Radial Distortion Coefficients.
        /// </summary>
        public Vector2 radialCoefficients;
        /// <summary>
        /// Principal point normalized, expressed as an offset from the view center.
        /// </summary>
        public Vector2 centerPoint;
    }

    /// <summary>
    /// Lens Distortion post process implementing the Brown–Conrady distortion model.
    /// </summary>
    [Serializable, VolumeComponentMenu("Post-processing/Custom/stYpe/Lens Distortion Brown-Conrady")]
    sealed public class LensDistortionVolumeComponent : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        static class ShaderIDs
        {
            public static readonly int _InputTexture = Shader.PropertyToID("_InputTexture");
            public static readonly int _OutOfViewportColor = Shader.PropertyToID("_OutOfViewportColor");
            public static readonly int _LensDistortionMainParams = Shader.PropertyToID("_LensDistortionMainParams");
            public static readonly int _SensorSize               = Shader.PropertyToID("_SensorSize");
            
            // Grid Visualization.
            public static readonly int _GridColor = Shader.PropertyToID("_GridColor");
            public static readonly int _GridResolution = Shader.PropertyToID("_GridResolution");
            public static readonly int _GridLineWidth = Shader.PropertyToID("_GridLineWidth");
        }

        const float k_DefaultOverScanScale = 1f;

        [Tooltip("The ratio between the distorted and undistorted focal lengths.")]
        public FloatParameter OverScanScale = new(k_DefaultOverScanScale);

        [Tooltip("Radial Distortion Coefficients.")]
        public Vector2Parameter RadialDistortionCoefficients = new (Vector2.zero);

        [Tooltip("Principal point normalized, expressed as an offset from the view center.")]
        public Vector2Parameter PrincipalPoint = new (Vector2.zero);

        [Tooltip("Color of the pixels lying outside of the viewport.")]
        public ColorParameter OutOfViewportColor = new (Color.black);

        [Tooltip("Show a grid to visualize lens distortion.")]
        public BoolParameter ShowGrid = new (false);

        [Tooltip("Color of the distortion visualization grid.")]
        public ColorParameter GridColor = new (Color.yellow);

        [Tooltip("Resolution of the distortion visualization grid.")]
        public FloatParameter GridResolution = new (12);

        [Tooltip("Thickness of the distortion visualization grid.")]
        public FloatParameter GridLineWidth = new (6);

        Material m_Material;
        
        public bool IsActive() => m_Material != null;

        /// <inheritdoc/>
        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        /// <inheritdoc/>
        public override void Setup()
        {
            var shader = Shader.Find("Hidden/Stype/Hdrp/LensDistortionBrownConrady");
            Assert.IsNotNull(shader);
            m_Material = new Material(shader);
        }

        /// <inheritdoc/>
        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            if (m_Material == null)
                return;

            if (camera.camera.cameraType == CameraType.SceneView)
            {
                HDUtils.BlitCameraTexture(cmd, source, destination);
                return;
            }
            
            m_Material.SetVector(ShaderIDs._OutOfViewportColor, OutOfViewportColor.value);
            ApplyLensDistortionParamsToMaterial(m_Material, OverScanScale.value,RadialDistortionCoefficients.value, 
                PrincipalPoint.value, camera);

            if (ShowGrid.value)
            {
                m_Material.SetColor(ShaderIDs._GridColor, GridColor.value);
                m_Material.SetFloat(ShaderIDs._GridResolution, GridResolution.value);
                m_Material.SetFloat(ShaderIDs._GridLineWidth, GridLineWidth.value);
            }

            m_Material.SetTexture(ShaderIDs._InputTexture, source);

            var pass = ShowGrid.value ? 1 : 0;
            HDUtils.DrawFullScreen(cmd, m_Material, destination, null, pass);
        }
        
        internal static void ApplyLensDistortionParamsToMaterial(Material mat, float overscanScale, 
            Vector2 radialCoefficients, Vector2 centerPoint, HDCamera camera)
        {
            // all are calculated with mm, not meters.
            mat.SetVector(ShaderIDs._LensDistortionMainParams, new Vector4(radialCoefficients.x, 
                radialCoefficients.y, centerPoint.x, centerPoint.y));

            Vector2 sensorSize = camera.camera.sensorSize;
            mat.SetVector(ShaderIDs._SensorSize, new Vector4(sensorSize.x, sensorSize.y, 0, 0));
        }

        public static void ApplyLensDistortionParamsToMaterial(Material mat, ref LensDistortionStypeData data, HDCamera camera)
        {
            // all are calculated with mm, not meters.
            mat.SetVector(ShaderIDs._LensDistortionMainParams, new Vector4(data.radialCoefficients.x,
                data.radialCoefficients.y, data.centerPoint.x, data.centerPoint.y));

            Vector2 sensorSize = camera.camera.sensorSize;
            mat.SetVector(ShaderIDs._SensorSize, new Vector4(sensorSize.x, sensorSize.y, 0, 0));
        }

        /// <inheritdoc/>
        public override void Cleanup() => CoreUtils.Destroy(m_Material);
    }
}
#endif
