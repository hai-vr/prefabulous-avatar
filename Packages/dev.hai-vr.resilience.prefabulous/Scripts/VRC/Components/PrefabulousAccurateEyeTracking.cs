using System;
using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.VRC.Runtime
{
    [AddComponentMenu("Prefabulous/PA-VRC Accurate Eye Tracking Transforms")]
    public class PrefabulousAccurateEyeTracking : MonoBehaviour, IEditorOnly
    {
        public Transform leftEyeYaw;
        public Transform leftEyePitch;
        public Transform rightEyeYaw;
        public Transform rightEyePitch;

        public PrefabulousAccurateEyeTrackingAnimatorVendor vendor = PrefabulousAccurateEyeTrackingAnimatorVendor.Adjerry91;
        
        public string leftXParam = "OSCm/Proxy/FT/v2/EyeLeftX";
        public string rightXParam = "OSCm/Proxy/FT/v2/EyeRightX";
        public string leftYParam = "OSCm/Proxy/FT/v2/EyeY";
        public string rightYParam = "OSCm/Proxy/FT/v2/EyeY";
    }
    
    [Serializable]
    public enum PrefabulousAccurateEyeTrackingAnimatorVendor
    {
        Custom, Adjerry91
    }
}
