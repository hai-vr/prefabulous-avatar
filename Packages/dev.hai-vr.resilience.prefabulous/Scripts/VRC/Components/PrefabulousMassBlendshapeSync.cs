using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.VRC.Runtime
{
    [AddComponentMenu("Prefabulous/PA-VRC Mass Blendshape Sync")]
    public class PrefabulousMassBlendshapeSync : MonoBehaviour, IEditorOnly
    {
        public SkinnedMeshRenderer source;
        public SkinnedMeshRenderer[] targets;
    }
}