using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace Prefabulous.VRC.Runtime
{
    [AddComponentMenu("Prefabulous/PA-VRC Import Expression Parameters")]
    [HelpURL("https://docs.hai-vr.dev/redirect/components/PrefabulousImportExpressionParameters")]
    public class PrefabulousImportExpressionParameters : MonoBehaviour, IEditorOnly
    {
        public VRCExpressionParameters parameters;
    }
}
