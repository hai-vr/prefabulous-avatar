using UnityEngine;

namespace Prefabulous.Hai.Runtime
{
    [AddComponentMenu("Prefabulous Avatar/PA-H Mass Blendshape Sync")]
    public class PrefabulousHaiMassBlendshapeSync : MonoBehaviour
    {
        public SkinnedMeshRenderer source;
        public SkinnedMeshRenderer[] targets;
    }
}