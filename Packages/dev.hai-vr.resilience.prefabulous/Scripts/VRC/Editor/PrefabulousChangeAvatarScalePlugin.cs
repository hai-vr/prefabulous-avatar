using nadena.dev.ndmf;
using Prefabulous.VRC.Editor;
using Prefabulous.VRC.Runtime;
using UnityEngine;

[assembly: ExportsPlugin(typeof(PrefabulousChangeAvatarScalePlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousChangeAvatarScalePlugin : Plugin<PrefabulousChangeAvatarScalePlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
                .Run("Change Avatar Scale", ctx =>
                {
                    var my = ctx.AvatarRootTransform.GetComponentInChildren<PrefabulousChangeAvatarScale>(true);
                    if (my == null) return;

                    var effectiveSourceSizeInMeters = my.customSourceSize ? my.sourceSizeInMeters : ctx.AvatarDescriptor.ViewPosition.y;
                    
                    Debug.Log($"({GetType().Name}) Rescaling from {effectiveSourceSizeInMeters:0.000}m to {my.desiredSizeInMeters:0.000}m");

                    var ratio = my.desiredSizeInMeters / effectiveSourceSizeInMeters;
                    ctx.AvatarRootTransform.localScale *= ratio;
                    ctx.AvatarDescriptor.ViewPosition *= ratio;
                    
                    PrefabulousUtil.DestroyAllAfterBake<PrefabulousChangeAvatarScale>(ctx);
                });
        }
    }
}