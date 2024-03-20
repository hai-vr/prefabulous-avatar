using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.Hai.Runtime
{
    [AddComponentMenu("Prefabulous Avatar/PA-H Accurate Eye Tracking")]
    public class PrefabulousHaiAccurateEyeTracking : MonoBehaviour, IEditorOnly
    {
        public Transform leftEyeYaw;
        public Transform leftEyePitch;
        public Transform rightEyeYaw;
        public Transform rightEyePitch;
    }
}