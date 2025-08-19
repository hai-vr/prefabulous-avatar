using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.VRC.Runtime
{
    [AddComponentMenu("Prefabulous/PA-VRC Mass Blendshape Sync")]
    [HelpURL("https://docs.hai-vr.dev/redirect/components/PrefabulousMassBlendshapeSync")]
    public class PrefabulousMassBlendshapeSync : MonoBehaviour, IEditorOnly
    {
        public SkinnedMeshRenderer source;
        public SkinnedMeshRenderer[] targets;
    }
}