using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.VRC.Runtime
{
#if PREFABULOUS_INTERNAL
    [AddComponentMenu("Prefabulous/PA-VRC Toe Tracking")]
#else
    [AddComponentMenu("")]
#endif
    public class PrefabulousToeTracking : MonoBehaviour, IEditorOnly
    {
    }
}