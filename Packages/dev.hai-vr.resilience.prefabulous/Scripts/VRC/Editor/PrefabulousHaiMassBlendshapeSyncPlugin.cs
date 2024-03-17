using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using Prefabulous.Hai.Runtime;
using Prefabulous.VRC.Editor;
using UnityEditor;

[assembly: ExportsPlugin(typeof(PrefabulousMassBlendshapeSyncPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousMassBlendshapeSyncPlugin : Plugin<PrefabulousMassBlendshapeSyncPlugin>
    {
        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Transforming)
                .AfterPlugin("Hai.FaceTraShape.Editor.HFTSCPlugin");
            
            seq.Run("Create Mass Blendshape Sync component", GenerateBlendshapes);
        }

        private void GenerateBlendshapes(BuildContext context)
        {
            var configs = context.AvatarRootTransform.GetComponentsInChildren<PrefabulousHaiMassBlendshapeSync>(true);
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
        }
    }
}