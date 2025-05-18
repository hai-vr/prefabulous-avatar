using System.Linq;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
using AnimatorAsCode.V1.VRC;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using Prefabulous.Universal.Shared.Editor;
using Prefabulous.VRC.Editor;
using Prefabulous.VRC.Runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

[assembly: ExportsPlugin(typeof(PrefabulousLockLocomotionMenuItemPlugin))]
namespace Prefabulous.VRC.Editor
{
#if PREFABULOUS_NDMF_CROSSAPP_INTEGRATION_SUPPORTED
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
#endif
    public class PrefabulousLockLocomotionMenuItemPlugin : Plugin<PrefabulousLockLocomotionMenuItemPlugin>
    {
        public override string QualifiedName => "dev.hai-vr.prefabulous.vrc.LockLocomotionMenuItem";
        public override string DisplayName => "Prefabulous for VRChat - Lock Locomotion Menu Item";
        
        public const string ParameterName = "Prefabulous/Hai/LockLocomotion";
        
        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .Run("Create Lock Locomotion", Generate);
        }

        private void Generate(BuildContext ctx)
        {
            var prefabulousComps = ctx.AvatarRootTransform.GetComponentsInChildren<PrefabulousLockLocomotionMenuItem>(true);
            if (prefabulousComps.Length == 0) return;

            CreateFxLayer(ctx, prefabulousComps.First());

            foreach (var comp in prefabulousComps)
            {
                // Add missing ModularAvatarMenuItem when applicable
                if (comp.transform.GetComponent<ModularAvatarMenuItem>() == null)
                {
                    comp.gameObject.AddComponent<ModularAvatarMenuItem>();
                }

                var menu = comp.transform.GetComponent<ModularAvatarMenuItem>();
                menu.hideFlags = HideFlags.NotEditable;
                menu.Control.icon = comp.icon;
                menu.Control.parameter.name = ParameterName;
                menu.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;

                var menuGroup = comp.transform.GetComponentInParent<ModularAvatarMenuGroup>();
                if (menuGroup == null)
                {
                    var source = comp.transform;
                    var holder = new GameObject
                    {
                        transform =
                        {
                            localPosition = source.transform.localPosition,
                            localRotation = source.transform.localRotation,
                            localScale = source.transform.localScale,
                            parent = source.parent
                        },
                        name = $"[{source.name}]"
                    };
                    source.SetParent(holder.transform, true);
                    holder.AddComponent<ModularAvatarMenuGroup>();
                    holder.AddComponent<ModularAvatarMenuInstaller>();
                }
            }

            PrefabulousUtil.DestroyAllAfterBake<PrefabulousLockLocomotionMenuItem>(ctx);
        }

        private void CreateFxLayer(BuildContext ctx, PrefabulousLockLocomotionMenuItem first)
        {
            var aac = AacV1.Create(new AacConfiguration
            {
                SystemName = GetType().Name,
                AnimatorRoot = ctx.AvatarRootTransform,
                DefaultValueRoot = ctx.AvatarRootTransform,
                AssetKey = GUID.Generate().ToString(),
                AssetContainer = ctx.AssetContainer,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
#if PREFABULOUS_NDMF_SUPPORTS_ASSETSAVER
                AssetContainerProvider = new PrefabulousAsCodeContainerProvider(ctx),
#endif
                DefaultsProvider = new AacDefaultsProvider(true)
            });

            var my = first.gameObject;
            
            var nonDestructiveFx = aac.NewAnimatorController();
            var fx = nonDestructiveFx.NewLayer();
            
            var param = fx.BoolParameter(ParameterName);
            var inactive = fx.NewState("Inactive").LocomotionEnabled();
            var active = fx.NewState("Active").LocomotionDisabled();
            inactive.TransitionsTo(active).When(param.IsTrue());
            active.TransitionsTo(inactive).When(param.IsFalse());

            var ma = MaAc.Create(my.gameObject);
            ma.NewParameter(param).NotSaved().NotSynced();
            ma.NewMergeAnimator(nonDestructiveFx, VRCAvatarDescriptor.AnimLayerType.FX);
            
            // FIXME: Expose Match WD in AAC
            my.gameObject.GetComponent<ModularAvatarMergeAnimator>().matchAvatarWriteDefaults = true;
        }
    }
}