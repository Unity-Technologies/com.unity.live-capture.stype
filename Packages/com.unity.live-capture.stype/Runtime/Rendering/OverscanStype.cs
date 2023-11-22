using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
#if HDRP_14_0_OR_NEWER
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Unity.LiveCapture.Stype.Rendering
{
    /// <summary>
    /// A component that manages the Lens Distortion effect.
    /// </summary>
    /// <remarks>
    /// This component uses the Brown-Conrady distortion model and is only available in HDRP.
    /// </remarks>
    [AddComponentMenu("Live Capture/Overscan (stYpe)")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
#if HDRP_14_0_OR_NEWER
    [RequireComponent(typeof(Camera))]
#endif
    [RequireComponent(typeof(HDAdditionalCameraData))]
    public class OverscanStype : MonoBehaviour
    {
        const string k_UsingOverscan = "USING_OVERSCAN";
        const string k_OverscanCameraName = "Overscan Camera";
        const string k_TargetName = "Overscan";
        static readonly int k_ScaleBiasShaderProperty = Shader.PropertyToID("_ScaleAndBias");
        const string k_CommandBufferName = "Crop And Present Overscanned Target";
        static readonly int k_RecorderTempRT = Shader.PropertyToID("TempRecorder");
        static readonly Vector4 k_VerticalFlipScaleAndBias = new(1, -1, 0, 1);
        static readonly Vector4 k_NotVerticalFlipScaleAndBias = new(1, 1, 0, 0);

        [SerializeField, HideInInspector]
        Camera m_OverscanCamera;
        [SerializeField, Tooltip("The amount of overscan.")]
        float m_Overscan;


        [Tooltip("Principal point normalized, expressed as an offset from image center.")]
        Camera m_Camera;
#if HDRP_14_0_OR_NEWER
        HDAdditionalCameraData m_AdditionalCameraData;
        HDAdditionalCameraData m_DstAdditionalCameraData;
#endif
        GlobalKeyword m_UsingOverscanKeyword;
        RenderTexture m_RenderTexture;
        Vector4 m_ScaleAndBias;
        bool m_UsePhysicalProperties;
        bool m_FlipVertical = false;
        Vector2? m_ViewportSize;

        /// <summary>
        /// The number of pixels to overscan.
        /// </summary>
        public float Overscan
        {
            get => m_Overscan;
            set => m_Overscan = value;
        }

        public Camera OverScanCamera => m_OverscanCamera;

        public void UpdateProperties()
        {
            if (m_OverscanCamera == null)
            {
                return;
            }

            var overscanTransform = m_OverscanCamera.transform;

            overscanTransform.position = transform.position;
            overscanTransform.rotation = transform.rotation;

            CopyCameraProperties(m_Camera, m_OverscanCamera);
        }

        public void SetResolutionForNextRendering(Vector2Int? resolution = null, bool flipVertical = true)
        {
            m_ViewportSize = resolution;
            m_FlipVertical = flipVertical;
        }

        void OnValidate()
        {
            m_Overscan = Mathf.Max(1f, m_Overscan);
        }

        void OnEnable()
        {
            EnsureOverscanCamera();
            m_UsingOverscanKeyword = GlobalKeyword.Create(k_UsingOverscan);
            m_Camera = GetComponent<Camera>();
#if HDRP_14_0_OR_NEWER
            m_AdditionalCameraData = GetComponent<HDAdditionalCameraData>();
            m_AdditionalCameraData.customRender += OnCustomRender;
#endif
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
            RenderPipelineManager.endContextRendering += OnEndContextRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.endContextRendering -= OnEndContextRendering;
#if HDRP_14_0_OR_NEWER
            m_AdditionalCameraData.customRender -= OnCustomRender;
#endif
            DisposeOverscanCamera();
            SafeReleaseAndDestroy(ref m_RenderTexture);
        }

        void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            var camera = m_OverscanCamera;
            if (camera == null)
            {
                return;
            }
            Vector2 viewportSize = m_ViewportSize ?? new Vector2(m_Camera.pixelWidth, m_Camera.pixelHeight);
            var textureSize = viewportSize * m_Overscan;
            var lensShift = camera.GetGateFittedLensShift();

            m_UsePhysicalProperties = camera.usePhysicalProperties;

            if (!m_UsePhysicalProperties
                && TryGetComponent<LensDistortionStype>(out var component)
                && component.enabled)
            {
                // Lens distortion needs lens shift regardless of the PhysicalCamera mode.
                lensShift = camera.lensShift;
            }

            // Unset targetTexture to avoid error:
            // "Releasing render texture that is set as Camera.targetTexture!"
            camera.targetTexture = null;

            // Allocate source target with overscan.
            m_RenderTexture = AllocateIfNeeded(m_RenderTexture, k_TargetName, textureSize, GetGraphicsFormat());

            // Calculate expanded projection with overscan.
            var normalizedViewportSubsection = Expand(
                new Rect(lensShift.x, lensShift.y, 1f, 1f),
                (m_Overscan - 1f) * 0.5f * Vector2.one);

            var projectionMatrix = Matrix4x4.Perspective(camera.GetGateFittedFieldOfView(), camera.aspect, camera.nearClipPlane, camera.farClipPlane);
            var frustum = SliceFrustum(projectionMatrix.decomposeProjection, normalizedViewportSubsection);

            projectionMatrix = Matrix4x4.Frustum(frustum);

            // Calculate blit parameters to crop out overscanned pixel on present.
            m_ScaleAndBias = new Vector4(
                viewportSize.x / textureSize.x,
                viewportSize.y / textureSize.y,
                viewportSize.x * 0.5f * (m_Overscan - 1f) / textureSize.x,
                viewportSize.y * 0.5f * (m_Overscan - 1f) / textureSize.y);

            camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;
            camera.projectionMatrix = projectionMatrix;
            camera.targetTexture = m_RenderTexture;

            // TODO Avoid global uniforms?
            Shader.EnableKeyword(m_UsingOverscanKeyword);
            Shader.SetGlobalVector(k_ScaleBiasShaderProperty, m_ScaleAndBias);
        }

        void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            Shader.DisableKeyword(m_UsingOverscanKeyword);
            Shader.SetGlobalVector(k_ScaleBiasShaderProperty, new Vector4(1f, 1f, 0f, 0f));

            var camera = m_OverscanCamera;

            if (camera == null)
            {
                return;
            }

            camera.ResetAspect();
            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();

            camera.usePhysicalProperties = m_UsePhysicalProperties;
        }

#if HDRP_14_0_OR_NEWER
        void OnCustomRender(ScriptableRenderContext scriptableRenderContext, HDCamera hdCamera)
        {
            if (m_RenderTexture != null && m_RenderTexture.IsCreated())
            {
                var camera = hdCamera.camera;
                var targetTexture = camera.targetTexture;
                var targetId = targetTexture != null ?
                    new RenderTargetIdentifier(targetTexture) :
                    new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);

                var cmd = CommandBufferPool.Get(k_CommandBufferName);
                var scaleBiasRT = m_FlipVertical ? k_VerticalFlipScaleAndBias : k_NotVerticalFlipScaleAndBias;

                var captureActions = CameraCaptureBridge.GetCaptureActions(camera);

                if (captureActions != null)
                {
                    // Blit while cropping out overscan pixels.
                    cmd.GetTemporaryRT(k_RecorderTempRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, GetGraphicsFormat());
                    cmd.SetRenderTarget(k_RecorderTempRT);

                    HDUtils.BlitQuad(cmd, m_RenderTexture, m_ScaleAndBias, scaleBiasRT, 0, true);

                    for (captureActions.Reset(); captureActions.MoveNext();)
                    {
                        var action = captureActions.Current;

                        action.Invoke(k_RecorderTempRT, cmd);
                    }
                }

                // Blit while cropping out overscan pixels.
                // When we render directly to game view, we render the image flipped up-side-down, like other HDRP cameras
                cmd.SetRenderTarget(targetId);

                HDUtils.BlitQuad(cmd, m_RenderTexture, m_ScaleAndBias, scaleBiasRT, 0, true);

                scriptableRenderContext.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }
#endif

        void LateUpdate()
        {
            m_ViewportSize = null;
#if HDRP_14_0_OR_NEWER
            m_FlipVertical = true;
#else
            m_FlipVertical = false;
#endif
            UpdateProperties();
        }

        void EnsureOverscanCamera()
        {
            if (m_OverscanCamera == null)
            {
                var go = new GameObject(k_OverscanCameraName, typeof(Camera));

                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;

                m_OverscanCamera = go.GetComponent<Camera>();
            }

            m_DstAdditionalCameraData = m_OverscanCamera.GetComponent<HDAdditionalCameraData>();
            if (m_DstAdditionalCameraData == null)
            {
                m_DstAdditionalCameraData = m_OverscanCamera.gameObject.AddComponent<HDAdditionalCameraData>();
            }
        }

        void DisposeOverscanCamera()
        {
            if (m_OverscanCamera != null)
            {
                AdditionalCoreUtils.DestroyIfNeeded(m_OverscanCamera.gameObject);

                m_OverscanCamera = null;
            }
        }

        void CopyCameraProperties(Camera src, Camera dst)
        {
            dst.allowDynamicResolution = src.allowDynamicResolution;
            dst.allowHDR = src.allowHDR;
            dst.allowMSAA = src.allowMSAA;
            dst.anamorphism = src.anamorphism;
            dst.aperture = src.aperture;
            dst.aspect = src.aspect;
            dst.backgroundColor = src.backgroundColor;
            dst.barrelClipping = src.barrelClipping;
            dst.bladeCount = src.bladeCount;
            dst.cameraType = src.cameraType;
            dst.clearFlags = src.clearFlags;
            dst.clearStencilAfterLightingPass = src.clearStencilAfterLightingPass;
            dst.cullingMask = src.cullingMask;
            dst.cullingMatrix = src.cullingMatrix;
            dst.curvature = src.curvature;
            dst.depthTextureMode = src.depthTextureMode;
            dst.enabled = src.enabled;
            dst.farClipPlane = src.farClipPlane;
            dst.fieldOfView = src.fieldOfView;
            dst.focalLength = src.focalLength;
            dst.focusDistance = src.focusDistance;
            dst.gateFit = src.gateFit;
            dst.iso = src.iso;
            dst.layerCullDistances = src.layerCullDistances;
            dst.lensShift = src.lensShift;
            dst.nearClipPlane = src.nearClipPlane;
            dst.nonJitteredProjectionMatrix = src.nonJitteredProjectionMatrix;
            dst.sensorSize = src.sensorSize;
            dst.projectionMatrix = src.projectionMatrix;
            dst.usePhysicalProperties = src.usePhysicalProperties;

#if HDRP_14_0_OR_NEWER
            m_AdditionalCameraData.CopyTo(m_DstAdditionalCameraData);
#endif
        }

        static GraphicsFormat GetGraphicsFormat()
        {
            var format = GraphicsFormat.R8G8B8A8_UNorm;

#if HDRP_14_0_OR_NEWER
            if (GraphicsSettings.renderPipelineAsset is HDRenderPipelineAsset asset)
            {
                format = (GraphicsFormat)asset.currentPlatformRenderPipelineSettings.colorBufferFormat;
            }
#endif

            return format;
        }

        static Rect Expand(Rect r, Vector2 delta)
        {
            return Rect.MinMaxRect(
                r.min.x - delta.x,
                r.min.y - delta.y,
                r.max.x + delta.x,
                r.max.y + delta.y);
        }

        static RenderTexture AllocateIfNeeded(RenderTexture rt, string name, Vector2 size, GraphicsFormat format)
        {
            int width = (int)size.x;
            int height = (int)size.y;

            if (rt == null ||
                rt.width != width ||
                rt.height != height ||
                rt.graphicsFormat != format)
            {
                SafeReleaseAndDestroy(ref rt);

                rt = new RenderTexture(width, height, 32, format, 0)
                {
                    name = $"{name}-{size.x}X{size.y}"
                };
            }

            return rt;
        }

        static void SafeReleaseAndDestroy(ref RenderTexture renderTexture)
        {
            if (renderTexture != null && renderTexture.IsCreated())
            {
                renderTexture.Release();
            }

            AdditionalCoreUtils.DestroyIfNeeded(ref renderTexture);
        }

        static FrustumPlanes SliceFrustum(FrustumPlanes frustumPlanes, Rect normalizedViewportSubsection)
        {
            return new FrustumPlanes
            {
                zNear = frustumPlanes.zNear,
                zFar = frustumPlanes.zFar,
                left = Mathf.LerpUnclamped(frustumPlanes.left, frustumPlanes.right, normalizedViewportSubsection.xMin),
                right = Mathf.LerpUnclamped(frustumPlanes.left, frustumPlanes.right, normalizedViewportSubsection.xMax),
                bottom = Mathf.LerpUnclamped(frustumPlanes.bottom, frustumPlanes.top, normalizedViewportSubsection.yMin),
                top = Mathf.LerpUnclamped(frustumPlanes.bottom, frustumPlanes.top, normalizedViewportSubsection.yMax)
            };
        }
    }
}
