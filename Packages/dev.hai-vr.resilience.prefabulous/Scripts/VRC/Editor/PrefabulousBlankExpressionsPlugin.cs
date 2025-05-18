﻿using nadena.dev.ndmf;
using Prefabulous.Universal.Shared.Editor;
using Prefabulous.VRC.Editor;
using Prefabulous.VRC.Runtime;
using VRC.SDK3.Avatars.ScriptableObjects;

[assembly: ExportsPlugin(typeof(PrefabulousBlankExpressionsPlugin))]
namespace Prefabulous.VRC.Editor
{
#if PREFABULOUS_NDMF_CROSSAPP_INTEGRATION_SUPPORTED
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
#endif
    public class PrefabulousBlankExpressionsPlugin : Plugin<PrefabulousBlankExpressionsPlugin>
    {
        public override string QualifiedName => "dev.hai-vr.prefabulous.vrc.BlankExpressions";
        public override string DisplayName => "Prefabulous for VRChat - Blank Expressions";
        
        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Blank Expressions Menu and Parameters", ctx =>
                {
                    var my = ctx.AvatarRootTransform.GetComponentInChildren<PrefabulousBlankExpressions>(true);
                    if (my == null) return;

                    ctx.AvatarDescriptor.expressionsMenu = new VRCExpressionsMenu();
                    ctx.AvatarDescriptor.expressionParameters = new VRCExpressionParameters();
                    
                    PrefabulousUtil.DestroyAllAfterBake<PrefabulousBlankExpressions>(ctx);
                });
        }
    }
}