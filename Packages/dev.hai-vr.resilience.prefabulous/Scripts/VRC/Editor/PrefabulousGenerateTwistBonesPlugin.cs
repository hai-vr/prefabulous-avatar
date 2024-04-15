using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Prefabulous.Universal.Common.Runtime;
using Prefabulous.Universal.Shared.Editor;
using Prefabulous.VRC.Editor;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

[assembly: ExportsPlugin(typeof(PrefabulousGenerateTwistBonesPlugin))]
namespace Prefabulous.Universal.Shared.Editor
{
    public class PrefabulousGenerateTwistBonesPlugin : Plugin<PrefabulousGenerateTwistBonesPlugin>
    {
        public override string QualifiedName => "dev.hai-vr.prefabulous.generate-twist-bones";
        public override string DisplayName => "Prefabulous Universal - Generate Twist Bones";
        
        protected override void Configure()
        {
            {
                InPhase(BuildPhase.Generating)
                    .Run("Generate Twist Bones (in Generating)", context =>
                    {
                        GenerateTwistBones(context, BuildPhase.Generating);
                    });
            }
            {
                // NOTE TO SELF: The build phase needs to move to Generating, in order to support both Modular Avatar Merge Armature and VRCFury Armature Link.
                // When moved into Generating, we need to create twist bones on each separate armatures independently.
                var seq = InPhase(BuildPhase.Transforming)
                    .AfterPlugin("nadena.dev.modular-avatar"); // Merge Armature must run first
                // FIXME:
                // Currently:
                // - We only create the twist bones once on the main avatar armature.
                // - All applicable meshes get skinned to those twist bones.
                // - We need the blendshapes of bracelets and wristwatches to still exist so that we can process those vertex IDs.
                // - We run this after Modular Avatar Merge Armature, but before VRCFury Armature Link.
                // - However, we need to run this *after* VRCFury Armature Link, and before any optimizations are applied.
                //
                // How do we run this after Modular Avatar "Merge Armature",
                // but also after VRCFury "Armature Link", while also being
                // before d4rkAvatarOptimizer, anatawaAvatarOptimizer and VRCFury merge mesh data
                // and remove wristwatch blendshapes???

                seq.Run("Generate Twist Bones (in post-MA Transforming)", context =>
                {
                    GenerateTwistBones(context, BuildPhase.Transforming);
                });
            }
            {
                InPhase(BuildPhase.Optimizing)
                    .Run("Generate Twist Bones (in Optimizing)", context =>
                    {
                        GenerateTwistBones(context, BuildPhase.Optimizing);
                        PrefabulousUtil.DestroyAllAfterBake<PrefabulousGenerateTwistBones>(context);
                    });
            }
        }
        
        private struct TwistBoneDefinition
        {
            public Vector3 upperUpSuggestion;
            public Vector3 lowerUpSuggestion;
            public Transform upper;
            public Transform lower;
            public Transform tip;
            public AnimationCurve weightDistribution;
            public bool createConstraint;
        }

        private static TwistBoneDefinition FromCustomComponent(PrefabulousGenerateTwistBones comp)
        {
            return new TwistBoneDefinition
            {
                upperUpSuggestion = comp.upperUpSuggestion,
                lowerUpSuggestion = comp.lowerUpSuggestion,
                upper = comp.upper,
                lower = comp.lower,
                tip = comp.tip,
                weightDistribution = comp.weightDistribution,
                createConstraint = comp.isMainArmature
            };
        }

        private void GenerateTwistBones(BuildContext context, BuildPhase phase)
        {
            var components = context.AvatarRootTransform.GetComponentsInChildren<PrefabulousGenerateTwistBones>(true)
                .Where(comp =>
                {
                    if (phase == BuildPhase.Generating) return !comp.generateInOptimizingPhase && comp.generateBeforeModularAvatarMergeArmature;
                    if (phase == BuildPhase.Transforming) return !comp.generateInOptimizingPhase && !comp.generateBeforeModularAvatarMergeArmature;
                    if (phase == BuildPhase.Optimizing) return comp.generateInOptimizingPhase;
                    return false;
                })
                .ToArray();
            if (components.Length == 0) return;
            
            var leftElbowJointLowerArmNullable = components.FirstOrDefault(that => that.leftElbowJointLowerArm);
            var rightElbowJointLowerArmNullable = components.FirstOrDefault(that => that.rightElbowJointLowerArm);

            var animator = context.AvatarRootTransform.GetComponent<Animator>();
            
            var tbs = new List<TwistBoneDefinition>();
            if (leftElbowJointLowerArmNullable != null)
            {
                tbs.Add(new TwistBoneDefinition
                {
                    upperUpSuggestion = Vector3.forward,
                    lowerUpSuggestion = Vector3.forward,
                    upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                    lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                    tip = animator.GetBoneTransform(HumanBodyBones.LeftHand),
                    weightDistribution = leftElbowJointLowerArmNullable.weightDistribution,
                    createConstraint = true
                });
            }
            if (rightElbowJointLowerArmNullable != null)
            {
                tbs.Add(new TwistBoneDefinition
                {
                    upperUpSuggestion = Vector3.forward,
                    lowerUpSuggestion = Vector3.forward,
                    upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                    lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                    tip = animator.GetBoneTransform(HumanBodyBones.RightHand),
                    weightDistribution = rightElbowJointLowerArmNullable.weightDistribution,
                    createConstraint = true
                });
            }
            
            foreach (var comp in components)
            {
                if (comp.useCustom)
                {
                    tbs.Add(FromCustomComponent(comp));
                }
            }

            if (tbs.Count == 0) return;

            var excludeBlendshapes = new HashSet<string>(components
                .SelectMany(bones => bones.excludeBraceletsAndWristwatchesBlendshapes)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct());

            var smrs = context.AvatarRootTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var tb in tbs)
            {
                ProcessTwistBone(tb, smrs, excludeBlendshapes);
            }
        }

        private static void ProcessTwistBone(TwistBoneDefinition tb, SkinnedMeshRenderer[] smrs, HashSet<string> excludeBlendshapes)
        {
            var applicableSmrs = smrs
                .Where(renderer => renderer.sharedMesh != null)
                .Where(renderer => renderer.bones.Contains(tb.lower))
                .ToArray();
            if (applicableSmrs.Length == 0) return;

            var twistBone = CreateTwistBoneObject(tb.lower);
            var objectUpSuggestion = new GameObject
            {
                transform =
                {
                    parent = tb.upper,
                    // The initial pose of the avatar will matter here.
                    position = tb.upper.position + tb.upperUpSuggestion * 0.2f * Vector3.Distance(tb.upper.position, tb.lower.position),
                    localRotation = Quaternion.identity,
                    localScale = Vector3.one,
                    // The name starts with Z, in order to prevent a possible problem with IK solvers that rely on alphabetical order.
                    // As far as I know, the alphabetical ordering might not be a confirmed issue in Unity, it might just be a precaution from creating bones in Blender,
                    // so that the twist bones in Unity are ordered to be last.
                    name = $"Z{tb.lower.name}TwistUpSuggestion"
                }
            }.transform;

            if (tb.createConstraint)
            {
                var aim = twistBone.gameObject.AddComponent<AimConstraint>();
                aim.upVector = twistBone.InverseTransformDirection(tb.lowerUpSuggestion).normalized;
                aim.aimVector = twistBone.InverseTransformDirection(tb.tip.position - tb.lower.position).normalized;
                aim.worldUpType = AimConstraint.WorldUpType.ObjectUp;
                aim.worldUpObject = objectUpSuggestion;
                aim.AddSource(new ConstraintSource
                {
                    weight = 1f,
                    sourceTransform = tb.tip
                });
                aim.constraintActive = true;
            }

            // Just in case the user has the same skinned mesh multiple times on the same avatar, process the mesh reference only once.
            var meshToSkinnedMeshes = applicableSmrs
                .GroupBy(renderer => renderer.sharedMesh)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.ToArray());

            foreach (var meshToSmr in meshToSkinnedMeshes)
            {
                var anyChange = ProcessMeshData(tb, meshToSmr.Key, meshToSmr.Value, excludeBlendshapes);
                if (anyChange)
                {
                    foreach (var smr in meshToSmr.Value)
                    {
                        smr.bones = smr.bones.Concat(new[] { twistBone }).ToArray();
                    }
                }
            }
        }

        private static bool ProcessMeshData(TwistBoneDefinition tb, Mesh mesh, SkinnedMeshRenderer[] skinnedMeshesOfThatMesh, HashSet<string> excludeBlendshapes)
        {
            var firstSmr = skinnedMeshesOfThatMesh[0];
            var futureTwistBoneIndex = firstSmr.bones.Length;

            var lowerIndex = firstSmr.bones.ToList().IndexOf(tb.lower);
            var lowerBindpose = mesh.bindposes[lowerIndex];
            PrefabulousUtil.ExtractFromTRS(Matrix4x4.Inverse(lowerBindpose), out var lowerPos, out _, out _);
            
            var tipIndex = firstSmr.bones.ToList().IndexOf(tb.tip); // FIXME: Maybe some SMRs don't have the tip?

            // Painting is done in mesh space.
            // Mesh space may have a different coordinate system, scale, or may be offset from the avatar.
            // We do all the vertex calculations in mesh space.
            Vector3 tipPos;
            if (tipIndex == -1)
            {
                if (!FindTipPosUsingSceneTip(tb.tip, mesh, firstSmr, out tipPos))
                {
                    return false;
                }
            }
            else
            {
                var tipBindpose = mesh.bindposes[tipIndex];
                PrefabulousUtil.ExtractFromTRS(Matrix4x4.Inverse(tipBindpose), out tipPos, out _, out _);
            }

            var vertexIndicesToExclude = excludeBlendshapes.Count > 0 
                ? FindAllVerticesToExclude(mesh, excludeBlendshapes)
                : new HashSet<int>();

            var unit = Vector3.Distance(lowerPos, tipPos);
            var lowerToTipUnitVec = (tipPos - lowerPos) / unit;

            var copy = Object.Instantiate(mesh);
            
            var vertices = copy.vertices;
            var boneCountPerVertex = copy.GetBonesPerVertex();
            var boneWeights = copy.GetAllBoneWeights();
            var vertexIndicesToModify = new Dictionary<int, List<BoneWeight1>>();
            var boneWeightAnchor = 0;
            for (var vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                if (vertexIndicesToExclude.Contains(vertexIndex)) continue;
                
                var vertexPos = vertices[vertexIndex];
                var boneCountOfVertex = boneCountPerVertex[vertexIndex];
                
                // For each vertex, iterate over its BoneWeights
                if (IsVertexPartOfLowerBone(boneWeights, boneCountOfVertex, boneWeightAnchor, lowerIndex))
                {
                    var lowerToVertexVec = (vertexPos - lowerPos) / unit;
                    
                    // 0 is closer to the lower bone, 1 is closer to the tip bone.
                    var dot = Vector3.Dot(lowerToTipUnitVec, lowerToVertexVec);

                    var weightNeedsChanging = dot < 1f;
                    if (weightNeedsChanging)
                    {
                        var newWeights = CopyBoneWeights(boneWeights, boneCountOfVertex, boneWeightAnchor);

                        if (dot < 0)
                        {
                            // Reassign Lower weight to Twist bone
                            for (var index = 0; index < newWeights.Count; index++)
                            {
                                var weight = newWeights[index];
                                if (weight.boneIndex == lowerIndex)
                                {
                                    weight.boneIndex = futureTwistBoneIndex;
                                }
                                newWeights[index] = weight;
                            }
                        }
                        else // (dot is between 0 and 1 non-inclusive)
                        {
                            // Share weight between Twist and Lower
                            var powerOfLower = tb.weightDistribution.Evaluate(dot);
                            var powerOfTwist = 1 - powerOfLower;

                            var existingWeight = 0f;
                            
                            for (var index = 0; index < newWeights.Count; index++)
                            {
                                var weight = newWeights[index];
                                if (weight.boneIndex == lowerIndex)
                                {
                                    existingWeight = weight.weight;
                                    weight.weight = existingWeight * powerOfLower;
                                }
                                newWeights[index] = weight;
                            }
                            
                            newWeights.Add(new BoneWeight1
                            {
                                boneIndex = futureTwistBoneIndex,
                                weight = existingWeight * powerOfTwist
                            });
                        }
                        
                        // SetBoneWeights documentation:
                        // "The bone weights for each vertex must be sorted with the most significant weights first."
                        newWeights.Sort(ComparerByMostSignificantWeight);
                        
                        vertexIndicesToModify.Add(vertexIndex, newWeights);
                    }
                }

                boneWeightAnchor += boneCountOfVertex;
            }

            if (vertexIndicesToModify.Count == 0) return false;

            var newAllBoneWeightsNativeArray = RecreateVertexArray(vertices.Length, boneWeights, boneCountPerVertex, vertexIndicesToModify);
            var newBoneCountPerVertex = RecreateBoneCountPerVertexArray(boneCountPerVertex, vertexIndicesToModify);
            
            copy.bindposes = copy.bindposes.Concat(new[] { lowerBindpose }).ToArray();
            copy.SetBoneWeights(newBoneCountPerVertex, newAllBoneWeightsNativeArray);

            foreach (var smr in skinnedMeshesOfThatMesh)
            {
                smr.sharedMesh = copy;
            }

            return true;
        }

        private static HashSet<int> FindAllVerticesToExclude(Mesh mesh, HashSet<string> excludeBlendshapes)
        {
            var vertexIndicesToExclude = new HashSet<int>();
            var verts = new Vector3[mesh.vertexCount];
            var norms = new Vector3[mesh.vertexCount];
            var tans = new Vector3[mesh.vertexCount];
            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                var blendShapeName = mesh.GetBlendShapeName(i);
                if (excludeBlendshapes.Contains(blendShapeName))
                {
                    var frameCount = mesh.GetBlendShapeFrameCount(i);
                    mesh.GetBlendShapeFrameVertices(i, frameCount - 1, verts, norms, tans);
                    for (var vertexIndex = 0; vertexIndex < verts.Length; vertexIndex++)
                    {
                        var vert = verts[vertexIndex];
                        if (vert != Vector3.zero)
                        {
                            vertexIndicesToExclude.Add(vertexIndex);
                        }
                    }
                }
            }

            return vertexIndicesToExclude;
        }

        private static bool FindTipPosUsingSceneTip(Transform tip, Mesh mesh, SkinnedMeshRenderer firstSmr, out Vector3 tipPos)
        {
            // Some mesh may not have a tip bone, i.e. if it's a biikini
            Debug.Log($"(PrefabulousGenerateTwistBones) Mesh {mesh.name} of SMR {firstSmr.name} has no {tip.name} bone. We will try to recalculate the tip bone from the hip bone.");
            var hipBone = firstSmr.bones[0];
            if (hipBone == null)
            {
                Debug.LogError($"(PrefabulousGenerateTwistBones) This mesh has a null hip bone. We cannot continue: Mesh {mesh.name} of SMR {firstSmr.name} will not use twist bones.");
                tipPos = Vector3.zero;
                return false;
            }
            var tipPosInWorldSpace = tip.position;
            var tipPosInHipSpace = hipBone.InverseTransformPoint(tipPosInWorldSpace);
            var meshBindpose = mesh.bindposes[0];
            var bindposeMatrixInverse = Matrix4x4.Inverse(meshBindpose);
                
            var tipPosInMeshSpace = bindposeMatrixInverse * ToVector4WithW1(tipPosInHipSpace); // W must be 1
            tipPos = tipPosInMeshSpace;
            return true;
        }

        private static NativeArray<BoneWeight1> RecreateVertexArray(int vertexCount, NativeArray<BoneWeight1> boneWeights, NativeArray<byte> boneCountPerVertex, Dictionary<int, List<BoneWeight1>> vertexIndicesToModify)
        {
            var newAllBoneWeights = new List<BoneWeight1>();
            var boneWeightAnchor = 0;
            for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                var boneCountOfVertex = boneCountPerVertex[vertexIndex];
                    
                if (vertexIndicesToModify.TryGetValue(vertexIndex, out var values))
                {
                    newAllBoneWeights.AddRange(vertexIndicesToModify[vertexIndex]);
                }
                else
                {
                    for (var spliceIndex = 0; spliceIndex < boneCountOfVertex; spliceIndex++)
                    {
                        // TODO: We cound copy a range instead of adding individually
                        newAllBoneWeights.Add(boneWeights[boneWeightAnchor + spliceIndex]);
                    }
                }

                boneWeightAnchor += boneCountOfVertex;
            }

            return new NativeArray<BoneWeight1>(newAllBoneWeights.ToArray(), Allocator.Temp);
        }

        private static Vector4 ToVector4WithW1(Vector3 vertex)
        {
            return new Vector4(vertex.x, vertex.y, vertex.z, 1);
        }

        private static NativeArray<byte> RecreateBoneCountPerVertexArray(NativeArray<byte> boneCountPerVertex, Dictionary<int, List<BoneWeight1>> vertexIndicesToModify)
        {
            var newBoneCountPerVertex = boneCountPerVertex.ToArray();
            foreach (var vertexIdToWeights in vertexIndicesToModify)
            {
                var valueCount = vertexIdToWeights.Value.Count; // FIXME: Unlikely but this could go over the max value, so it needs trimming.
                newBoneCountPerVertex[vertexIdToWeights.Key] = (byte)valueCount;
            }

            return new NativeArray<byte>(newBoneCountPerVertex, Allocator.Temp);
        }

        private static int ComparerByMostSignificantWeight(BoneWeight1 a, BoneWeight1 b)
        {
            return b.weight.CompareTo(a.weight);
        }

        private static List<BoneWeight1> CopyBoneWeights(NativeArray<BoneWeight1> boneWeights, byte boneCountOfVertex, int boneWeightAnchor)
        {
            var boneWeight1s = new List<BoneWeight1>();
            for (var spliceIndex = 0; spliceIndex < boneCountOfVertex; spliceIndex++)
            {
                var currentBoneWeight = boneWeights[boneWeightAnchor + spliceIndex];
                boneWeight1s.Add(currentBoneWeight);
            }

            return boneWeight1s;
        }

        private static bool IsVertexPartOfLowerBone(NativeArray<BoneWeight1> boneWeights, byte boneCountOfVertex, int boneWeightAnchor, int lowerIndex)
        {
            for (var spliceIndex = 0; spliceIndex < boneCountOfVertex; spliceIndex++)
            {
                var currentBoneWeight = boneWeights[boneWeightAnchor + spliceIndex];
                var isVertexPartOfLowerBone = currentBoneWeight.boneIndex == lowerIndex;
                if (isVertexPartOfLowerBone) return true;
            }

            return false;
        }

        private static Transform CreateTwistBoneObject(Transform lower)
        {
            return new GameObject
            {
                transform =
                {
                    parent = lower,
                    localPosition = Vector3.zero,
                    localRotation = Quaternion.identity,
                    localScale = Vector3.one,
                    // The name starts with Z, in order to prevent a possible problem with IK solvers that rely on alphabetical order.
                    // As far as I know, the alphabetical ordering might not be a confirmed issue in Unity, it might just be a precaution from creating bones in Blender,
                    // so that the twist bones in Unity are ordered to be last.
                    name = $"Z{lower.name}Twist"
                }
            }.transform;
        }
    }
}