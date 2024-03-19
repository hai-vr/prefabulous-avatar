using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Prefabulous.Hai.Runtime;
using Prefabulous.VRC.Editor;
using UnityEngine;
using UnityEngine.Rendering;

[assembly: ExportsPlugin(typeof(PrefabulousHaiDeletePolygonsPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousHaiDeletePolygonsPlugin : Plugin<PrefabulousHaiDeletePolygonsPlugin>
    {
        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Optimizing);
            
            seq.Run("Delete Polygons", DeletePolygons)
                .BeforePlugin("com.anatawa12.avatar-optimizer");
        }

        private void DeletePolygons(BuildContext context)
        {
            var deletePolygons = context.AvatarRootTransform.GetComponentsInChildren<PrefabulousHaiDeletePolygons>(true);
            if (deletePolygons.Length == 0) return;

            var smrs = context.AvatarRootTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                
                var thatSmrBlendShapes = Enumerable.Range(0, mesh.blendShapeCount)
                    .Select(i => mesh.GetBlendShapeName(i))
                    .ToList();
                var applicableBlendShapes = deletePolygons
                    .Where(recalculate => !recalculate.limitToSpecificMeshes || recalculate.renderers.Contains(smr))
                    .SelectMany(recalculate => recalculate.blendShapes)
                    .Where(blendShape => thatSmrBlendShapes.Contains(blendShape))
                    .Distinct()
                    .ToList();
                var keepPartialBlendshapes = deletePolygons
                    .Where(recalculate => recalculate.keepPartialPolygons)
                    .Where(recalculate => !recalculate.limitToSpecificMeshes || recalculate.renderers.Contains(smr))
                    .SelectMany(recalculate => recalculate.blendShapes)
                    .Where(blendShape => thatSmrBlendShapes.Contains(blendShape))
                    .Distinct()
                    .ToList();
                if (applicableBlendShapes.Count > 0)
                {
                    DeletePolygonsOf(smr, thatSmrBlendShapes, applicableBlendShapes, keepPartialBlendshapes);
                }
            }
            
            PrefabulousUtil.DestroyAllAfterBake<PrefabulousHaiDeletePolygons>(context);
        }

        private void DeletePolygonsOf(SkinnedMeshRenderer smr, List<string> thatSmrBlendShapes,
            List<string> applicableBlendShapes, List<string> keepPartialBlendshapes)
        {
            // TODO: If multiple SMRs share the same sharedMesh, it may not be necessary to do this op on all of them
            // However, it's rare for a single avatar to be referencing the same SMR mesh mutliple times.
            var originalMesh = smr.sharedMesh;

            PrefabulousUtil.FigureOutAffectedVertices(out var verticesToDelete, out var partialVertices, thatSmrBlendShapes, applicableBlendShapes, keepPartialBlendshapes, originalMesh);

            var remap = Enumerable.Range(0, originalMesh.vertexCount)
                .Where(i => !verticesToDelete[i])
                .ToArray();
            var oldIndexToNewIndex = new Dictionary<int, int>();
            for (var newVertexIndex = 0; newVertexIndex < remap.Length; newVertexIndex++)
            {
                oldIndexToNewIndex[remap[newVertexIndex]] = newVertexIndex;
            }

            smr.sharedMesh = InstantiateNewMeshAndDeleteVerticesFromMesh(originalMesh, verticesToDelete, partialVertices, oldIndexToNewIndex, smr.name);
        }

        private Mesh InstantiateNewMeshAndDeleteVerticesFromMesh(Mesh originalMesh, bool[] verticesToDelete, bool[] partialVertices, Dictionary<int, int> oldIndexToNewIndex, string smrName)
        {
            var mesh = new Mesh();
            
            var verts = new Vector3[originalMesh.vertexCount];
            var norms = new Vector3[originalMesh.vertexCount];
            var tans = new Vector3[originalMesh.vertexCount];

            mesh.bindposes = originalMesh.bindposes;
            mesh.name = originalMesh.name;
            mesh.bounds = originalMesh.bounds;

            var newVertexCount = verticesToDelete.Count(b => !b);
            Debug.Log($"({GetType().Name}) Deleting {originalMesh.vertexCount - newVertexCount} vertices (out of {originalMesh.vertexCount}) from {smrName}");

            Fromd4rk.SetMeshIndexFormat(mesh, newVertexCount);
            mesh.SetVertices(NewArrayDeleteVerts(originalMesh.vertices, verticesToDelete));
            mesh.SetNormals(NewArrayDeleteVerts(originalMesh.normals, verticesToDelete));
            mesh.SetTangents(NewArrayDeleteVerts(originalMesh.tangents, verticesToDelete));
            
            mesh.boneWeights = NewArrayDeleteVerts(originalMesh.boneWeights, verticesToDelete);

            var channels = Enumerable.Range(0, 8)
                .Select(uvChannel =>
                {
                    var result = new List<Vector4>();
                    originalMesh.GetUVs(uvChannel, result);

                    if (result.Count != originalMesh.vertexCount)
                    {
                        return new Vector4[originalMesh.vertexCount];
                    }
                    return result.ToArray();
                })
                .Select(uvs => NewArrayDeleteVerts(uvs, verticesToDelete))
                .ToArray();

            Fromd4rk.SetUVs(mesh, channels);

            if (originalMesh.HasVertexAttribute(VertexAttribute.Color))
            {
                if (originalMesh.GetVertexAttributeFormat(VertexAttribute.Color) == VertexAttributeFormat.UNorm8)
                {
                    mesh.colors32 = NewArrayDeleteVerts(originalMesh.colors32, verticesToDelete);
                }
                else
                {
                    mesh.colors = NewArrayDeleteVerts(originalMesh.colors, verticesToDelete);
                }
            }

            mesh.subMeshCount = originalMesh.subMeshCount;
            
            for (var subMeshIndex = 0; subMeshIndex < originalMesh.subMeshCount; subMeshIndex++)
            {
                var indices = originalMesh.GetIndices(subMeshIndex);
                var newIndices = NewTrianglesDeleteVerts(indices, verticesToDelete, partialVertices, oldIndexToNewIndex);
                mesh.SetIndices(newIndices, MeshTopology.Triangles, subMeshIndex);
            }

            var originalTriangleCount = originalMesh.triangles.Length;
            Debug.Log($"({GetType().Name}) Deleting {(originalTriangleCount - mesh.triangles.Length) / 3} triangles (out of {originalTriangleCount / 3}) from {smrName}");

            for (var shapeIndex = 0; shapeIndex < originalMesh.blendShapeCount; shapeIndex++)
            {
                var name = originalMesh.GetBlendShapeName(shapeIndex);
                var frames = originalMesh.GetBlendShapeFrameCount(shapeIndex);
                for (var frameIndex = 0; frameIndex < frames; frameIndex++)
                {
                    var weight = originalMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                    originalMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, verts, norms, tans);

                    var newDeltaVerts = NewArrayDeleteVerts(verts, verticesToDelete);
                    var newDeltaNorms = NewArrayDeleteVerts(norms, verticesToDelete);
                    var newDeltaTans = NewArrayDeleteVerts(tans, verticesToDelete);
                    mesh.AddBlendShapeFrame(name, weight, newDeltaVerts, newDeltaNorms, newDeltaTans);
                }
            }

            return mesh;
        }

        private static T[] NewArrayDeleteVerts<T>(T[] originalVertices, bool[] verticesToDelete)
        {
            return originalVertices
                .Where((_, vertexIndex) => !verticesToDelete[vertexIndex])
                .ToArray();
        }

        private int[] NewTrianglesDeleteVerts(int[] triangles, bool[] verticesToDelete, bool[] partialVertices,
            Dictionary<int, int> oldIndexToNewIndex)
        {
            var originalTriangleTriads = triangles;
            var triangleCount = originalTriangleTriads.Length / 3;
            var newTriangles = Enumerable.Range(0, triangleCount)
                .SelectMany(triangleIndex =>
                {
                    var a = originalTriangleTriads[3 * triangleIndex];
                    var b = originalTriangleTriads[3 * triangleIndex + 1];
                    var c = originalTriangleTriads[3 * triangleIndex + 2];

                    var atLeastOneVertexDoesNotExist = verticesToDelete[a] || verticesToDelete[b] || verticesToDelete[c];
                    bool AreAllPartialVertices() => partialVertices[a] && partialVertices[b] && partialVertices[c];

                    var shouldDeleteTriangle = atLeastOneVertexDoesNotExist || AreAllPartialVertices();
                    if (shouldDeleteTriangle)
                    {
                        return Array.Empty<int>();
                    }

                    return new[] { oldIndexToNewIndex[a], oldIndexToNewIndex[b], oldIndexToNewIndex[c] };
                })
                .ToArray();

            return newTriangles;
        }
    }
}