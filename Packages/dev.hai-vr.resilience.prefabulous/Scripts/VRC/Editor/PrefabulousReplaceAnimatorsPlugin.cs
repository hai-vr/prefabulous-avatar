﻿using System;
using System.Linq;
using nadena.dev.ndmf;
using Prefabulous.Universal.Shared.Editor;
using Prefabulous.VRC.Editor;
using Prefabulous.VRC.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(PrefabulousReplaceAnimatorsPlugin))]
namespace Prefabulous.VRC.Editor
{
#if PREFABULOUS_NDMF_CROSSAPP_INTEGRATION_SUPPORTED
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
#endif
    public class PrefabulousReplaceAnimatorsPlugin : Plugin<PrefabulousReplaceAnimatorsPlugin>
    {
        public override string QualifiedName => "dev.hai-vr.prefabulous.vrc.ReplaceAnimators";
        public override string DisplayName => "Prefabulous for VRChat - Replace Animators";
        
        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Resolving)
                .BeforePlugin("nadena.dev.modular-avatar");
            
            seq.Run("Replace Locomotion Animator", context => ReplaceAnimator<PrefabulousReplaceLocomotionAnimator>(
                context,
                arg => arg.controller,
                VRCAvatarDescriptor.AnimLayerType.Base
            ));
            seq.Run("Replace Action Animator", context => ReplaceAnimator<PrefabulousReplaceActionAnimator>(
                context,
                arg => arg.controller,
                VRCAvatarDescriptor.AnimLayerType.Action
            ));
            seq.Run("Blank Gesture Animator", context => ReplaceAnimator<PrefabulousBlankGestureAnimator>(
                context,
                arg =>
                {
                    var ac = new AnimatorController();
                    ac.AddLayer("Base Layer");
                    var layers = ac.layers;
                    layers[0].avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Masks/vrc_HandsOnly.mask");
                    ac.layers = layers;
                    return ac;
                },
                VRCAvatarDescriptor.AnimLayerType.Gesture
            ));
            seq.Run("Blank FX Animator", context => ReplaceAnimator<PrefabulousBlankFXAnimator>(
                context,
                arg => new AnimatorController(),
                VRCAvatarDescriptor.AnimLayerType.FX
            ));
            seq.Run("Remove Replace Animators component group", context =>
            {
                PrefabulousUtil.DestroyAllAfterBake<PrefabulousReplaceLocomotionAnimator>(context);
                PrefabulousUtil.DestroyAllAfterBake<PrefabulousReplaceActionAnimator>(context);
                PrefabulousUtil.DestroyAllAfterBake<PrefabulousBlankGestureAnimator>(context);
                PrefabulousUtil.DestroyAllAfterBake<PrefabulousBlankFXAnimator>(context);
            });
        }

        private static void ReplaceAnimator<T>(BuildContext context, Func<T, RuntimeAnimatorController> getControllerFn, VRCAvatarDescriptor.AnimLayerType type)
        {
            var prefabulousComps = context.AvatarRootTransform.GetComponentsInChildren<T>(true);
            if (prefabulousComps.Length > 0)
            {
                var controller = getControllerFn(prefabulousComps.Last());
                context.AvatarDescriptor.baseAnimationLayers = context.AvatarDescriptor.baseAnimationLayers
                    .Select(layer => ReplaceAnimatorOfType(layer, controller, type))
                    .ToArray();
            }
        }

        private static VRCAvatarDescriptor.CustomAnimLayer ReplaceAnimatorOfType(VRCAvatarDescriptor.CustomAnimLayer layer, RuntimeAnimatorController replaceWith, VRCAvatarDescriptor.AnimLayerType type)
        {
            if (layer.type == type)
            {
                layer.animatorController = replaceWith;
                layer.isDefault = false;
            }

            return layer;
        }
    }
}