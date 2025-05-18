using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using Prefabulous.Universal.Shared.Editor;
using Prefabulous.VRC.Editor;
using Prefabulous.VRC.Runtime;
using UnityEditor;

[assembly: ExportsPlugin(typeof(PrefabulousMassBlendshapeSyncPlugin))]
namespace Prefabulous.VRC.Editor
{
#if PREFABULOUS_NDMF_CROSSAPP_INTEGRATION_SUPPORTED
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
#endif
    public class PrefabulousMassBlendshapeSyncPlugin : Plugin<PrefabulousMassBlendshapeSyncPlugin>
    {
        public override string QualifiedName => "dev.hai-vr.prefabulous.vrc.MassBlendshapeSync";
        public override string DisplayName => "Prefabulous for VRChat - Mass Blendshape Sync";
        
        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Transforming)
                .AfterPlugin("Hai.FaceTraShape.Editor.HFTSCPlugin");
            
            seq.Run("Create Mass Blendshape Sync component", GenerateBlendshapes);
        }

        private void GenerateBlendshapes(BuildContext context)
        {
            var configs = context.AvatarRootTransform.GetComponentsInChildren<PrefabulousMassBlendshapeSync>(true);
            foreach (var config in configs)
            {
                if (config.source == null) continue;
                if (config.source.sharedMesh.blendShapeCount == 0) continue;
                if (config.targets.All(renderer => renderer == null)) continue;

                var referencePath = AnimationUtility.CalculateTransformPath(config.source.transform, context.AvatarRootTransform);
                
                var foundInSource = new HashSet<string>(PrefabulousUtil.GetAllBlendshapeNames(config.source));
                foreach (var target in config.targets)
                {
                    if (target == null) continue;
                    
                    var foundInTarget = new HashSet<string>(PrefabulousUtil.GetAllBlendshapeNames(target));
                    foundInTarget.IntersectWith(foundInSource);

                    if (foundInTarget.Count > 0)
                    {
                        var bs = target.GetComponent<ModularAvatarBlendshapeSync>();
                        if (bs == null)
                        {
                            bs = target.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
                        }

                        var alreadyExistInBlendshapeSync = new HashSet<string>(bs.Bindings.Select(binding => binding.LocalBlendshape).ToArray());
                        foreach (var potential in foundInTarget)
                        {
                            if (!alreadyExistInBlendshapeSync.Contains(potential))
                            {
                                bs.Bindings.Add(new BlendshapeBinding
                                {
                                    Blendshape = potential,
                                    LocalBlendshape = potential,
                                    ReferenceMesh = new AvatarObjectReference
                                    {
                                        referencePath = referencePath
                                    }
                                });
                            }
                        }
                    }
                }
            }
                    
            PrefabulousUtil.DestroyAllAfterBake<PrefabulousMassBlendshapeSync>(context);
        }
    }
}