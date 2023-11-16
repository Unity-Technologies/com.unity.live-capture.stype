using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;



    public class CaptureScreenShot
    {
        [MenuItem("StypeValidation/Screen Capture")]
        static void CaptureScreen()
        {
            _CaptureScreen();
        }


        static void _CaptureScreen()
        {
            DateTime now = DateTime.Now;
            string timestamp = $"{ now.Year }-{ now.Month.ToString("D2") }{ now.Day.ToString("D2") }-"
                             + $"{ now.Hour.ToString("D2") }{ now.Minute.ToString("D2") }{ now.Second.ToString("D2") }";
            string filePath = $"{ Application.productName }-{ timestamp }.png";

            UnityEngine.ScreenCapture.CaptureScreenshot(filePath, 1);
        }
    }

#endif
