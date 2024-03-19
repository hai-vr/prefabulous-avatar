using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Prefabulous.Hai.Runtime;
using Prefabulous.VRC.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(PrefabulousHaiAssignUVTilePlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousHaiAssignUVTilePlugin : Plugin<PrefabulousHaiAssignUVTilePlugin>
    {
        private const float Offset = 0.5f;

        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Optimizing)
                .AfterPlugin<PrefabulousHaiDeletePolygonsPlugin>()
                .BeforePlugin("com.anatawa12.avatar-optimizer");

            seq.Run("Assign UV Tiles", AssignUVTIles);
        }

        private void AssignUVTIles(BuildContext context)
        {
            var assignUVTiles = context.AvatarRootTransform.GetComponentsInChildren<PrefabulousHaiAssignUVTile>(true);
            if (assignUVTiles.Length == 0) return;

            // Order matters: Process all components of type "EntireMesh", in case additional "BlendShapes" components will further split that assigned UV tile into more tiles.
            ProcessEntireMeshMode(assignUVTiles.Where(tile => tile.mode == PrefabulousHaiAssignUVTile.AssignMode.EntireMesh).ToArray());
            ProcessBlendshapesMode(context, assignUVTiles.Where(tile => tile.mode == PrefabulousHaiAssignUVTile.AssignMode.BlendShapes).ToArray());
            
            PrefabulousUtil.DestroyAllAfterBake<PrefabulousHaiAssignUVTile>(context);
        }

        private void ProcessEntireMeshMode(PrefabulousHaiAssignUVTile[] assignUVTiles_onlyEntireMeshMode)
        {
            foreach (var assignUVTile in assignUVTiles_onlyEntireMeshMode)
            {
                foreach (var renderer in assignUVTile.entireMeshes)
                {
                    if (renderer is SkinnedMeshRenderer smr)
                    {
                        var originalMesh = smr.sharedMesh;
                        if (originalMesh == null) continue;

                        smr.sharedMesh = CopyAndAssignEntirely(originalMesh, assignUVTile);

                    }
                    else if (renderer is MeshRenderer mr)
                    {
                        var mf = mr.GetComponent<MeshFilter>();
                        if (mf == null) continue;
                        
                        var originalMesh = mf.sharedMesh;
                        if (originalMesh == null) continue;

                        mf.sharedMesh = CopyAndAssignEntirely(originalMesh, assignUVTile);
                    }
                }
            }
        }

        private Mesh CopyAndAssignEntirely(Mesh originalMesh, PrefabulousHaiAssignUVTile assignUVTile)
        {
            var mesh = Object.Instantiate(originalMesh);
            
            var channelNumber = AsChannelNumber(assignUVTile.uvChannel);
            var existingData = assignUVTile.existingData;
            var u = assignUVTile.u;
            var v = assignUVTile.v;
            
            var vertexCount = originalMesh.vertexCount;

            var existingUvs = PrefabulousUtil.GetUVsDefensively(originalMesh, channelNumber);
            var uvs = existingData == PrefabulousHaiAssignUVTile.ExistingData.DoNotClear || existingData == PrefabulousHaiAssignUVTile.ExistingData.Shift
                ? existingUvs
                : Enumerable.Repeat(
                    existingData == PrefabulousHaiAssignUVTile.ExistingData.SetToZero
                        ? new Vector4(0 + Offset, 0 + Offset, 0f, 0f)
                        : new Vector4(-1 + Offset, -1 + Offset, 0f, 0f),
                    vertexCount).ToArray();

            var middleOfTile = new Vector4(u + Offset, v + Offset, 0f, 0f);
            var displacement = new Vector4(u, v, 0f, 0f);
            for (var index = 0; index < vertexCount; index++)
            {
                uvs[index] = existingData == PrefabulousHaiAssignUVTile.ExistingData.Shift
                    ? (existingUvs[index] + displacement)
                    : middleOfTile;
            }

            Fromd4rk.SetUV(mesh, channelNumber, uvs);
            
            return mesh;
        }

        private void ProcessBlendshapesMode(BuildContext context, PrefabulousHaiAssignUVTile[] assignUVTiles_onlyBlendshapeMode)
        {
            var smrs = context.AvatarRootTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;

                var thatSmrBlendShapes = Enumerable.Range(0, mesh.blendShapeCount)
                    .Select(i => mesh.GetBlendShapeName(i))
                    .ToList();
                var applicableBlendShapes = assignUVTiles_onlyBlendshapeMode
                    .Where(recalculate => !recalculate.limitToSpecificMeshes || recalculate.renderers.Contains(smr))
                    .SelectMany(uvTile =>
                    {
                        return uvTile.blendShapes
                            .Where(blendShape => thatSmrBlendShapes.Contains(blendShape))
                            .Distinct()
                            .Select(blendShape => new AssignUVTileIntermediate
                            {
                                blendShape = blendShape,
                                channel = uvTile.uvChannel,
                                u = uvTile.u,
                                v = uvTile.v,
                                existingData = uvTile.existingData
                            });
                    })
                    .ToList();
                var keepPartialBlendshapes = assignUVTiles_onlyBlendshapeMode
                    .Where(recalculate => recalculate.keepPartialPolygons)
                    .Where(recalculate => !recalculate.limitToSpecificMeshes || recalculate.renderers.Contains(smr))
                    .SelectMany(normals => normals.blendShapes)
                    .Where(blendShape => thatSmrBlendShapes.Contains(blendShape))
                    .Distinct()
                    .ToList();
                if (applicableBlendShapes.Count > 0)
                {
                    AssignBlendshapeUVTileOf(smr, thatSmrBlendShapes, applicableBlendShapes, keepPartialBlendshapes);
                }
            }
        }

        internal struct AssignUVTileIntermediate
        {
            public string blendShape;
            public PrefabulousHaiAssignUVTile.UVChannel channel;
            public int u;
            public int v;
            public PrefabulousHaiAssignUVTile.ExistingData existingData;
        }
        //
        // internal struct AssignUVTileWork
        // {
        //     public string blendShape;
        //     public Dictionary<PrefabulousHaiAssignUVTile.UVChannel, AssignUVTileChannels> channels;
        // }
        //
        // internal struct AssignUVTileChannels
        // {
        //     public int u;
        //     public int v;
        // }

        private void AssignBlendshapeUVTileOf(SkinnedMeshRenderer smr, List<string> thatSmrBlendShapes,
            List<AssignUVTileIntermediate> applicableBlendShapes, List<string> keepPartialBlendshapes)
        {
            // TODO: If multiple SMRs share the same sharedMesh, it may not be necessary to do this op on all of them
            // However, it's rare for a single avatar to be referencing the same SMR mesh mutliple times.
            var originalMesh = smr.sharedMesh;

            var mesh = Object.Instantiate(originalMesh);

            var channelToBlendShapes = applicableBlendShapes.GroupBy(intermediate => intermediate.channel).ToDictionary(grouping => grouping.Key, grouping => grouping.ToArray());
            foreach (var channelToBlendShape in channelToBlendShapes)
            {
                var uvChannel = channelToBlendShape.Key;
                var intermediatesOfThisChannel = channelToBlendShape.Value;
                
                var channelNumber = AsChannelNumber(uvChannel);
                var existingData = GetExistingData(intermediatesOfThisChannel);

                var existingUvs = PrefabulousUtil.GetUVsDefensively(originalMesh, channelNumber);
                var uvs = existingData == PrefabulousHaiAssignUVTile.ExistingData.DoNotClear || existingData == PrefabulousHaiAssignUVTile.ExistingData.Shift
                    ? existingUvs
                    : Enumerable.Repeat(
                        existingData == PrefabulousHaiAssignUVTile.ExistingData.SetToZero
                            ? new Vector4(0 + Offset, 0 + Offset, 0f, 0f)
                            : new Vector4(-1 + Offset, -1 + Offset, 0f, 0f),
                        originalMesh.vertexCount).ToArray();
                
                
                foreach (var intermediate in intermediatesOfThisChannel)
                {
                    var middleOfTile = new Vector4(intermediate.u + Offset, intermediate.v + Offset, 0f, 0f);
                    var displacement = new Vector4(intermediate.u, intermediate.v, 0f, 0f);
                    PrefabulousUtil.FigureOutAffectedVertices(out var verticesToDelete, out var partialVertices, thatSmrBlendShapes, new[] { intermediate.blendShape }.ToList(), keepPartialBlendshapes, originalMesh);
                    for (var index = 0; index < verticesToDelete.Length; index++)
                    {
                        var shouldAssign = verticesToDelete[index];
                        if (shouldAssign)
                        {
                            uvs[index] = existingData == PrefabulousHaiAssignUVTile.ExistingData.Shift
                                ? (existingUvs[index] + displacement)
                                : middleOfTile;
                        }
                    }
                }

                Fromd4rk.SetUV(mesh, channelNumber, uvs);
            }
            
            smr.sharedMesh = mesh;
        }

        private PrefabulousHaiAssignUVTile.ExistingData GetExistingData(AssignUVTileIntermediate[] intermediatesOfThisChannel)
        {
            var datas = intermediatesOfThisChannel
                .Select(intermediate => intermediate.existingData)
                .Distinct()
                .ToArray();
            if (datas.Contains(PrefabulousHaiAssignUVTile.ExistingData.SetToMinusOne)) return PrefabulousHaiAssignUVTile.ExistingData.SetToMinusOne;
            if (datas.Contains(PrefabulousHaiAssignUVTile.ExistingData.SetToZero)) return PrefabulousHaiAssignUVTile.ExistingData.SetToZero;
            if (datas.Contains(PrefabulousHaiAssignUVTile.ExistingData.Shift)) return PrefabulousHaiAssignUVTile.ExistingData.Shift;
            return PrefabulousHaiAssignUVTile.ExistingData.DoNotClear;
        }

        private static int AsChannelNumber(PrefabulousHaiAssignUVTile.UVChannel uvChannel)
        {
            return (int)uvChannel;
        }
    }
}