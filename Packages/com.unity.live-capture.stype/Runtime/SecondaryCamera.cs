using System;
using UnityEngine;
using UnityEngine.PlayerLoop;
using Unity.LiveCapture.Cameras;
using Unity.LiveCapture.Stype.Rendering;

namespace Unity.LiveCapture.Stype
{
    [RequireComponent(typeof(Camera))]

    public class SecondaryCamera : MonoBehaviour
    {
        [SerializeField] Camera m_primaryCamera;
        LensDistortionStype m_primaryLensDistortion;

        Camera m_secondaryCamera;
        FieldOfViewToFocalLength m_fovToFoculLength;
        LensDistortionStype m_secondaryLensDistortion;

        public Camera PrimaryCamera { get => m_primaryCamera; set => m_primaryCamera = value; }

        private void OnEnable()
        {
            if (m_primaryCamera == null)
                throw new InvalidOperationException("m_primaryCamera is null.");
            m_primaryLensDistortion = m_primaryCamera.GetComponent<LensDistortionStype>();

            m_secondaryCamera = GetComponent<Camera>();
            m_fovToFoculLength = m_secondaryCamera.GetComponent<FieldOfViewToFocalLength>();
            m_secondaryLensDistortion = m_secondaryCamera.GetComponent<LensDistortionStype>();

            PlayerLoopExtensions.RegisterUpdate<PreLateUpdate, SynchronizerUpdate>(OnSynchronizerUpdate, 1);
        }

        private void OnDisable()
        {
            PlayerLoopExtensions.DeregisterUpdate<SynchronizerUpdate>(OnSynchronizerUpdate);
        }

        void OnSynchronizerUpdate()
        {
            if (m_primaryCamera == null)
                throw new InvalidOperationException("m_primaryCamera is null.");
            if (m_secondaryCamera == null)
                throw new InvalidOperationException("m_secondaryCamera is null.");

            m_secondaryCamera.transform.position = m_primaryCamera.transform.position;
            m_secondaryCamera.transform.rotation = m_primaryCamera.transform.rotation;
            m_secondaryCamera.fieldOfView = m_primaryCamera.fieldOfView;
            m_fovToFoculLength.FieldOfView = m_primaryCamera.fieldOfView;
            m_secondaryCamera.focusDistance = m_primaryCamera.focusDistance;
            m_secondaryCamera.sensorSize = m_primaryCamera.sensorSize;
            m_secondaryCamera.gateFit = m_primaryCamera.gateFit;
            m_secondaryLensDistortion.CenterPoint = m_primaryLensDistortion.CenterPoint;
            m_secondaryLensDistortion.RadialCoefficients = m_primaryLensDistortion.RadialCoefficients;
        }
    }
}
