using System.Linq;
using nadena.dev.ndmf;
using Prefabulous.VRC.Editor;
using Prefabulous.VRC.Runtime;
using UnityEngine;

[assembly: ExportsPlugin(typeof(PrefabulousEditAllMeshBoundsPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousEditAllMeshBoundsPlugin : Plugin<PrefabulousEditAllMeshBoundsPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run("Edit Mesh Bounds", context =>
                {
                    var prefabulousComps = context.AvatarRootTransform.GetComponentsInChildren<PrefabulousEditAllMeshBounds>(true);
                    if (prefabulousComps.Length == 0) return;

                    var my = prefabulousComps.Last();

                    var smrs = context.AvatarRootTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    foreach (var smr in smrs)
                    {
                        smr.localBounds = my.bounds;
                    }
                    
                    PrefabulousUtil.DestroyAllAfterBake<PrefabulousEditAllMeshBounds>(context);
                });
        }
    }
}