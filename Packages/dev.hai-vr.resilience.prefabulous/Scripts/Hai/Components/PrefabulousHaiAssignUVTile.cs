using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.Hai.Runtime
{
    [AddComponentMenu("Prefabulous Avatar/PA-H Assign UV Tile")]
    public class PrefabulousHaiAssignUVTile : MonoBehaviour, IEditorOnly
    {
        public string[] blendShapes;
        public bool limitToSpecificMeshes;
        public SkinnedMeshRenderer[] renderers;

        public BoxCollider[] vertexContainers;
        
        public UVChannel uvChannel = UVChannel.UV1;
        public int u;
        public int v;
        
        public enum UVChannel {
            UV0, UV1, UV2, UV3
        }
    }
}