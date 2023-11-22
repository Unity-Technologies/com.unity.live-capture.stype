using Unity.LiveCapture.Cameras;
using UnityEngine;

namespace Unity.LiveCapture.Stype.Rendering
{
    /// <summary>
    /// A component that manages the Lens Distortion effect.
    /// </summary>
    /// <remarks>
    /// This component uses the Brown-Conrady distortion model and is only available in HDRP.
    /// </remarks>
    [AddComponentMenu("Live Capture/Lens Distortion (stYpe)")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(SharedVolumeProfile))]
    public class LensDistortionStype : MonoBehaviour
    {
        internal const string RadialCoefficientsFieldName = nameof(m_RadialCoefficients);
        internal const string CenterPointFieldName = nameof(m_CenterPoint);

        [SerializeField]
        Vector3 m_RadialCoefficients;
        [SerializeField]
        Vector2 m_CenterPoint;

        /// <summary>
        /// The radial distortion coefficients.
        /// </summary>
        public Vector3 RadialCoefficients
        {
            get => m_RadialCoefficients;
            set => m_RadialCoefficients = value;
        }

        /// <summary>
        /// Principal point normalized, expressed as an offset from the view center.
        /// </summary>
        public Vector2 CenterPoint
        {
            get => m_CenterPoint;
            set => m_CenterPoint = value;
        }

        /// <summary>
        /// Create a lens data to pass the LensDistortionVolumeComponent.ApplyLensDistortionParamsToMaterial method.
        /// </summary>
        /// <returns></returns>
        public LensDistortionStypeData GetData()
        {
            return new LensDistortionStypeData()
            {
                radialCoefficients = m_RadialCoefficients,
                centerPoint = m_CenterPoint,
            };
        }

        SharedVolumeProfile m_SharedVolumeProfile;

        void OnEnable()
        {
            m_SharedVolumeProfile = GetComponent<SharedVolumeProfile>();
        }

        void OnDisable()
        {
            SetActive(false);
        }

        void OnDestroy()
        {
            if (m_SharedVolumeProfile == null)
            {
                return;
            }
#if HDRP_14_0_OR_NEWER
            m_SharedVolumeProfile.DestroyVolumeComponent<LensDistortionVolumeComponent>();
#endif
        }

        void OnValidate()
        {
        }

        void SetActive(bool value)
        {
#if HDRP_14_0_OR_NEWER
            {
                if (m_SharedVolumeProfile.TryGetVolumeComponent<LensDistortionVolumeComponent>(out var lensDistortion))
                {
                    lensDistortion.active = value;
                }
            }
#endif
        }

        void LateUpdate()
        {
#if HDRP_14_0_OR_NEWER
            var lensDistortion = m_SharedVolumeProfile.GetOrCreateVolumeComponent<LensDistortionVolumeComponent>();

            lensDistortion.active = true;

            lensDistortion.RadialDistortionCoefficients.overrideState = true;
            lensDistortion.RadialDistortionCoefficients.value = m_RadialCoefficients;

            lensDistortion.PrincipalPoint.overrideState = true;
            lensDistortion.PrincipalPoint.value = m_CenterPoint;
#endif
        }
    }
}
