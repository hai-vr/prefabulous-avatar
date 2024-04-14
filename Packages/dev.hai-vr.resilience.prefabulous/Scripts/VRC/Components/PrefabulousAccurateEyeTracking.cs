using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.VRC.Runtime
{
#if PREFABULOUS_INTERNAL
    [AddComponentMenu("Prefabulous/PA-VRC Accurate Eye Tracking")]
#else
    [AddComponentMenu("")]
#endif
    public class PrefabulousAccurateEyeTracking : MonoBehaviour, IEditorOnly
    {
        public Transform leftEyeYaw;
        public Transform leftEyePitch;
        public Transform rightEyeYaw;
        public Transform rightEyePitch;
    }
}
