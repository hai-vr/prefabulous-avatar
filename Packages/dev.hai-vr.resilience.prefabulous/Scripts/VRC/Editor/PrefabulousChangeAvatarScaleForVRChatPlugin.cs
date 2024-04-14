using nadena.dev.ndmf;
using Prefabulous.Universal.Common.Runtime;
using Prefabulous.Universal.Shared.Editor;
using Prefabulous.VRC.Editor;
using UnityEngine;

[assembly: ExportsPlugin(typeof(PrefabulousChangeAvatarScaleForVRChatPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousChangeAvatarScaleForVRChatPlugin : Plugin<PrefabulousChangeAvatarScaleForVRChatPlugin>
    {
        public override string QualifiedName => "dev.hai-vr.prefabulous.vrc.ChangeAvatarScaleForVRChat";
        public override string DisplayName => "Prefabulous for VRChat - Change Avatar Scale (for VRChat)";
        
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