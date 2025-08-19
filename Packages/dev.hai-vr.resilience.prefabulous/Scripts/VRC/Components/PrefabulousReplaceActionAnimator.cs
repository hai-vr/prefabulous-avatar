using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.VRC.Runtime
{
    [AddComponentMenu("Prefabulous/PA-VRC Replace Action Animator")]
    [HelpURL("https://docs.hai-vr.dev/redirect/components/PrefabulousReplaceActionAnimator")]
    public class PrefabulousReplaceActionAnimator : MonoBehaviour, IEditorOnly
    {
        public RuntimeAnimatorController controller;
    }
}