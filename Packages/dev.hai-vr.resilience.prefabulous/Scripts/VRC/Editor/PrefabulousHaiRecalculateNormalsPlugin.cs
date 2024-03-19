using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Prefabulous.Hai.Runtime;
using Prefabulous.VRC.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(PrefabulousHaiRecalculateNormalsPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousHaiRecalculateNormalsPlugin : Plugin<PrefabulousHaiRecalculateNormalsPlugin>
    {
        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Transforming)
                .AfterPlugin("Hai.FaceTraShape.Editor.HFTSCPlugin")
                .AfterPlugin<PrefabulousHaiGenerateBlendshapesFTEPlugin>();
            
            seq.Run("Recalculate Normals", RecalculateNormals);
        }

        private void RecalculateNormals(BuildContext context)
        {
            var recalculates = context.AvatarRootTransform.GetComponentsInChildren<PrefabulousHaiRecalculateNormals>(true);
            if (recalculates.Length == 0) return;

            var smrs = context.AvatarRootTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                
                var thatSmrBlendShapes = Enumerable.Range(0, mesh.blendShapeCount)
                    .Select(i => mesh.GetBlendShapeName(i))
                    .ToList();
                var applicableBlendShapes = recalculates
                    .Where(recalculate => !recalculate.limitToSpecificMeshes || recalculate.renderers.Contains(smr))
                    .SelectMany(normals => normals.blendShapes)
                    .Where(blendShape => thatSmrBlendShapes.Contains(blendShape))
                    .Distinct()
                    .ToList();
                var eraseCustomSplitNormalsBlendShapes = recalculates
                    .Where(recalculate => recalculate.eraseCustomSplitNormals)
                    .Where(recalculate => !recalculate.limitToSpecificMeshes || recalculate.renderers.Contains(smr))
                    .SelectMany(normals => normals.blendShapes)
                    .Where(blendShape => thatSmrBlendShapes.Contains(blendShape))
                    .Distinct()
                    .ToList();
                if (applicableBlendShapes.Count > 0)
                {
                    RecalculateNormalsOf(smr, thatSmrBlendShapes, applicableBlendShapes, eraseCustomSplitNormalsBlendShapes);
                }
            }

            PrefabulousUtil.DestroyAllAfterBake<PrefabulousHaiRecalculateNormals>(context);
        }

        private void RecalculateNormalsOf(SkinnedMeshRenderer smr, List<string> thatSmrBlendShapes, List<string> applicableBlendShapes, List<string> eraseCustomSplitNormalsBlendShapes)
        {
            // TODO: If multiple SMRs share the same sharedMesh, it may not be necessary to do this op on all of them
            // However, it's rare for a single avatar to be referencing the same SMR mesh mutliple times.
            var originalMesh = smr.sharedMesh;
            
            var baker = Object.Instantiate(smr);
            for (var i = 0; i < thatSmrBlendShapes.Count; i++)
            {
                baker.SetBlendShapeWeight(i, 0);
            }

            var bonesInBindpose = originalMesh.bindposes
                .Select(bindPose =>
                {
                    var inverse = Matrix4x4.Inverse(bindPose);
                    var t = new GameObject
                    {
                        transform = { parent = baker.transform }
                    }.transform;
                    PrefabulousUtil.ExtractFromTRS(inverse, out var pos, out var rot, out var sca);
                    t.localPosition = pos;
                    t.localRotation = rot;
                    t.localScale = sca;

                    return t;
                })
                .ToArray();
            baker.bones = bonesInBindpose;

            var bake0mesh = new Mesh();
            baker.BakeMesh(bake0mesh);
            
            // RecalculateNormals fails to calculate normals aesthetically when an artist-authored vertex is part of an UV seam,
            // which turns it into multiple vertices in the engine mesh data representation.
            // To fix this, we store the indices that have the same position and normal (pre-recalculate normals),
            // and then reuse these indices to re-recalculate an "average normal", which isn't always accurate but is a good trade-off.
            // LEARN MORE: https://hai-vr.notion.site/Recalculate-Normals-Retrospective-e8b319e25c5a4b779c220a4d8286ded4
            var indicesWithSamePosNorm = StoreIndicesWithSamePositionAndNormal(bake0mesh);
            // We're recalculating normals on a 0-mesh because we want the delta to be based on the diff with Unity's RecalculateNormals judgement
            bake0mesh.RecalculateNormals();
            ReRecalculateNormalsInUVSeams(bake0mesh, indicesWithSamePosNorm);
            bake0mesh.RecalculateTangents();
            
            // Export normals and tangents now to avoid Unity extern access
            var originalNormals = originalMesh.normals;
            var originalTangents = originalMesh.tangents;
            
            // Export normals and tangents now to avoid Unity extern access
            var bake0MeshNormals = bake0mesh.normals;
            var bake0MeshTangents = bake0mesh.tangents;

            var nameToFrameDeltaBakes = new Dictionary<string, DeltaMeshBake[]>();

            var _ignored = new Vector3[originalMesh.vertexCount];
            var deltaVertices = new Vector3[originalMesh.vertexCount];
            var deltaNormals = new Vector3[originalMesh.vertexCount];
            var deltaTangents = new Vector3[originalMesh.vertexCount];

            foreach (var applicableBlendShape in applicableBlendShapes)
            {
                var shapeIndex = thatSmrBlendShapes.IndexOf(applicableBlendShape);
                var frameCount = originalMesh.GetBlendShapeFrameCount(shapeIndex);

                var meshBake = new DeltaMeshBake[frameCount];
                nameToFrameDeltaBakes[applicableBlendShape] = meshBake;
                
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var frameWeight = originalMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);

                    var bakedShape = new Mesh();
                
                    baker.SetBlendShapeWeight(shapeIndex, frameWeight);
                    baker.BakeMesh(bakedShape);
                    baker.SetBlendShapeWeight(shapeIndex, 0);
                
                    // We need to recalculate the indices because it's possible that a blendshape splits the vertices apart,
                    // even if they had the same PosNorm (i.e. something that opens clothing in half)
                    var indicesInBakedWithSamePosNorm = StoreIndicesWithSamePositionAndNormal(bakedShape);
                    bakedShape.RecalculateNormals();
                    ReRecalculateNormalsInUVSeams(bakedShape, indicesInBakedWithSamePosNorm);
                    bakedShape.RecalculateTangents();

                    // Export normals and tangents now to avoid Unity extern access
                    var bakedShapeNormals = bakedShape.normals;
                    var bakedShapeTangents = bakedShape.tangents;

                    originalMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, _ignored, _ignored);
                    for (var i = 0; i < originalMesh.vertexCount; i++)
                    {
                        // Zero-valued vertices still need normals and tangents updated based on neighbouring non-zero-valued vertices.
                        // This is especially visible on blendshapes that "flatten" something, where zero-valued vertices still need delta normals applied to avoid an "aura" bump.
                        // Therefore execute this even when the delta vertex is equal to zero.
                        deltaNormals[i] = bakedShapeNormals[i] - bake0MeshNormals[i];
                        deltaTangents[i] = (Vector3)(bakedShapeTangents[i] - bake0MeshTangents[i]); // TODO: What's the deal with the binormal (Vector4 tangents)?
                    }

                    if (eraseCustomSplitNormalsBlendShapes.Contains(applicableBlendShape))
                    {
                        var nonZero = 0;
                        var zero = 0;
                        for (var i = 0; i < originalMesh.vertexCount; i++)
                        {
                            // Only recalculate deltas on some vertices, based on the deltas we've calculated previously.
                            // Since this only affects some vertices, this prevents incorrect delta normals from contaminating unrelated vertices in the mesh.
                            if (deltaVertices[i] != Vector3.zero || deltaNormals[i] != Vector3.zero)
                            {
                                nonZero++;
                                // Erase custom split normals by using the original mesh rather than the base recalculated mesh
                                deltaNormals[i] = bakedShapeNormals[i] - originalNormals[i];
                                deltaTangents[i] = (Vector3)(bakedShapeTangents[i] - originalTangents[i]);
                            }
                            else
                            {
                                zero++;
                            }
                        }
                        Debug.Log($"({GetType().Name}) Erasing custom split normals on blendshape {applicableBlendShape} in SMR {smr.name} resulted in {nonZero} non-zero vertices and {zero} zero vertices");
                    }
                    
                    nameToFrameDeltaBakes[applicableBlendShape][frameIndex].vertices = deltaVertices.ToArray();
                    nameToFrameDeltaBakes[applicableBlendShape][frameIndex].normals = deltaNormals.ToArray();
                    nameToFrameDeltaBakes[applicableBlendShape][frameIndex].tangents = deltaTangents.ToArray();
                }
            }
            
            Object.DestroyImmediate(baker.gameObject);
            
            var mesh = Object.Instantiate(originalMesh);
            RebuildBlendshapesOnCopy(mesh, originalMesh, nameToFrameDeltaBakes);
            smr.sharedMesh = mesh;
        }

        private void ReRecalculateNormalsInUVSeams(Mesh mesh, List<int[]> indicesWithSamePosNorm)
        {
            // LEARN MORE: https://hai-vr.notion.site/Recalculate-Normals-Retrospective-e8b319e25c5a4b779c220a4d8286ded4
            var normals = mesh.normals;
            foreach (var indices in indicesWithSamePosNorm)
            {
                var normal = Vector3.zero;
                foreach (var i in indices)
                {
                    normal += normals[i];
                }
                normal /= indices.Length;
                normal = normal.normalized;
                
                foreach (var i in indices)
                {
                    normals[i] = normal;
                }
            }

            mesh.normals = normals;
        }

        private List<int[]> StoreIndicesWithSamePositionAndNormal(Mesh bake0Mesh)
        {
            // We do not want to include the tangent, because the tangent is determined by the UV seam.
            // (Correcting for UV seams is the main reason we're doing this)
            
            var vertices = bake0Mesh.vertices;
            var normals = bake0Mesh.normals;

            var posNormToIndex = new Dictionary<PosNorm, List<int>>();
            for (var index = 0; index < vertices.Length; index++)
            {
                var posNorm = new PosNorm(vertices[index], normals[index]);
                if (!posNormToIndex.ContainsKey(posNorm))
                {
                    posNormToIndex.Add(posNorm, new List<int>());
                }
                posNormToIndex[posNorm].Add(index);
            }

            return posNormToIndex
                .Where(pair => pair.Value.Count >= 2)
                .Select(pair => pair.Value.ToArray())
                .ToList();
        }

        internal struct PosNorm
        {
            public PosNorm(Vector3 position, Vector3 normal)
            {
                this.position = position;
                this.normal = normal;
            }

            public Vector3 position;
            public Vector3 normal;

            public bool Equals(PosNorm other)
            {
                return position.Equals(other.position) && normal.Equals(other.normal);
            }

            public override bool Equals(object obj)
            {
                return obj is PosNorm other && Equals(other);
            }

            public override int GetHashCode()
            {
#if UNITY_2022_1_OR_NEWER
                return System.HashCode.Combine(position, normal);

#else
                unchecked
                {
                    return (position.GetHashCode() * 397) ^ normal.GetHashCode();
                }
#endif
            }
        }

        private static void RebuildBlendshapesOnCopy(Mesh meshCopy, Mesh originalMesh, Dictionary<string, DeltaMeshBake[]> nameToFrameDeltaBakes)
        {
            var verts = new Vector3[originalMesh.vertexCount];
            var norms = new Vector3[originalMesh.vertexCount];
            var tans = new Vector3[originalMesh.vertexCount];

            meshCopy.ClearBlendShapes();
            for (var shapeIndex = 0; shapeIndex < originalMesh.blendShapeCount; shapeIndex++)
            {
                var name = originalMesh.GetBlendShapeName(shapeIndex);
                if (!nameToFrameDeltaBakes.Keys.Contains(name))
                {
                    var frames = originalMesh.GetBlendShapeFrameCount(shapeIndex);
                    for (var frameIndex = 0; frameIndex < frames; frameIndex++)
                    {
                        var weight = originalMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                        originalMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, verts, norms, tans);
                        meshCopy.AddBlendShapeFrame(name, weight, verts, norms, tans);
                    }
                }
                else
                {
                    var baked = nameToFrameDeltaBakes[name];
                    var frames = baked.Length;
                    for (var frameIndex = 0; frameIndex < frames; frameIndex++)
                    {
                        var weight = originalMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                        var bakedFrame = baked[frameIndex];
                        meshCopy.AddBlendShapeFrame(name, weight, bakedFrame.vertices, bakedFrame.normals, bakedFrame.tangents);
                    }
                }
            }
        }
    }

    internal struct DeltaMeshBake
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector3[] tangents;
    }
}