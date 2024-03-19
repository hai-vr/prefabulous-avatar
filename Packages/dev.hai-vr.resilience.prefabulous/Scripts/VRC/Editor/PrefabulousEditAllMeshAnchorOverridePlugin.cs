using System.Linq;
using nadena.dev.ndmf;
using Prefabulous.VRC.Editor;
using Prefabulous.VRC.Runtime;
using UnityEngine;

[assembly: ExportsPlugin(typeof(PrefabulousEditAllMeshAnchorOverridePlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousEditAllMeshAnchorOverridePlugin : Plugin<PrefabulousEditAllMeshAnchorOverridePlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run("Edit Mesh Anchor Override", context =>
                {
                    var prefabulousComps = context.AvatarRootTransform.GetComponentsInChildren<PrefabulousEditAllMeshAnchorOverride>(true);
                    if (prefabulousComps.Length == 0) return;

                    var my = prefabulousComps.Last();

                    var smrs = context.AvatarRootTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    foreach (var smr in smrs)
                    {
                        smr.probeAnchor = my.anchorOverride;
                    }

                    var mrs = context.AvatarRootTransform.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var mr in mrs)
                    {
                        mr.probeAnchor = my.anchorOverride;
                    }
                    
                    PrefabulousUtil.DestroyAllAfterBake<PrefabulousEditAllMeshAnchorOverride>(context);
                });
        }
    }
}