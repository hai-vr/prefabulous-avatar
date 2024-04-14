using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.VRC.Runtime
{
    [AddComponentMenu("Prefabulous/PA-VRC Replace Locomotion Animator")]
    public class PrefabulousReplaceLocomotionAnimator : MonoBehaviour, IEditorOnly
    {
        public RuntimeAnimatorController controller;
    }
}