using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.VRC.Runtime
{
    [AddComponentMenu("Prefabulous/PA-VRC Replace Locomotion Animator")]
    [HelpURL("https://docs.hai-vr.dev/redirect/components/PrefabulousReplaceLocomotionAnimator")]
    public class PrefabulousReplaceLocomotionAnimator : MonoBehaviour, IEditorOnly
    {
        public RuntimeAnimatorController controller;
    }
}