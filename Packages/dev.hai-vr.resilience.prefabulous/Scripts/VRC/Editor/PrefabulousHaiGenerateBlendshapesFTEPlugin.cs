using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Prefabulous.Hai.Runtime;
using Prefabulous.VRC.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(PrefabulousHaiGenerateBlendshapesFTEPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousHaiGenerateBlendshapesFTEPlugin : Plugin<PrefabulousHaiGenerateBlendshapesFTEPlugin>
    {
        public const string EyeClosedLeft = "EyeClosedLeft";
        public const string EyeClosedRight = "EyeClosedRight";
        public const string Body = "Body";
        public const string HaiXT_EyeClosedInverse_Smile = "HaiXT_EyeClosedInverse_Smile";
        private const string HaiXT_EyeClosedInverse_SmileLeft = HaiXT_EyeClosedInverse_Smile + "Left";
        private const string HaiXT_EyeClosedInverse_SmileRight = HaiXT_EyeClosedInverse_Smile + "Right";
        
        private int _vertexCount;
        private Vector3[] _storagePosition;
        private Vector3[] _storageNormal;
        private Vector3[] _storageTangent;
        private Vector3[] _deltaPosition;
        private Vector3[] _deltaNormal;
        private Vector3[] _deltaTangent;

        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Transforming)
                .AfterPlugin("Hai.FaceTraShape.Editor.HFTSCPlugin");
            
            seq.Run("Generate Blendshapes for Face Tracking Extensions", GenerateBlendshapes);
        }

        private void GenerateBlendshapes(BuildContext context)
        {
            var config = context.AvatarRootTransform.GetComponentInChildren<PrefabulousHaiGenerateBlendshapesFTE>(true);
            if (config == null) return;

            var body = context.AvatarRootTransform.Find(Body);
            if (body == null)
            {
                Debug.Log($"({GetType().Name}) No Body found, cannot generate any blendshape");
                return;
            }

            var smr = body.GetComponent<SkinnedMeshRenderer>();
            if (smr == null)
            {
                Debug.Log($"({GetType().Name}) No SkinnedMeshRenderer found in Body, cannot generate any blendshape");
                return;
            }

            var blendshapeNames = PrefabulousUtil.GetAllBlendshapeNames(smr);
            TryGenerateEyeClosedInverseSmile(smr, blendshapeNames, config);
            
            PrefabulousUtil.DestroyAllAfterBake<PrefabulousHaiGenerateBlendshapesFTE>(context);
        }

        private void TryGenerateEyeClosedInverseSmile(SkinnedMeshRenderer smr, string[] blendshapeNames, PrefabulousHaiGenerateBlendshapesFTE config)
        {
            if (!blendshapeNames.Contains(EyeClosedLeft) || !blendshapeNames.Contains(EyeClosedRight))
            {
                Debug.Log($"({GetType().Name}) Cannot generate {HaiXT_EyeClosedInverse_Smile} as required blendshapes {EyeClosedLeft} or {EyeClosedRight} is missing");
                return;
            }

            if (!blendshapeNames.Contains(config.EyeClosedInverse_Smile_EyeLeft) || !blendshapeNames.Contains(config.EyeClosedInverse_Smile_EyeRight))
            {
                Debug.Log($"({GetType().Name}) Cannot generate {HaiXT_EyeClosedInverse_Smile} as configuration blendshapes {config.EyeClosedInverse_Smile_EyeLeft} or {config.EyeClosedInverse_Smile_EyeRight} is missing");
                return;
            }

            if (blendshapeNames.Contains(HaiXT_EyeClosedInverse_SmileLeft) || blendshapeNames.Contains(HaiXT_EyeClosedInverse_SmileRight))
            {
                Debug.Log($"({GetType().Name}) Cannot generate {HaiXT_EyeClosedInverse_Smile} as the extensions blendshapes to be created already exist");
                return;
            }

            DoGenerateEyeClosedInverseSmile(smr, blendshapeNames, config.EyeClosedInverse_Smile_EyeLeft,config.EyeClosedInverse_Smile_EyeRight);
        }

        private void DoGenerateEyeClosedInverseSmile(SkinnedMeshRenderer smr, string[] blendshapeNames, string eyeSmileLeft, string eyeSmileRight)
        {
            // TODO: If I'm gonna add more extensions in the future, we need to instantiate the mesh and the arrays on the first passing blendshape.
            var mesh = Object.Instantiate(smr.sharedMesh);
            
            _vertexCount = mesh.vertexCount;
            _storagePosition = new Vector3[_vertexCount];
            _storageNormal = new Vector3[_vertexCount];
            _storageTangent = new Vector3[_vertexCount];
            _deltaPosition = new Vector3[_vertexCount];
            _deltaNormal = new Vector3[_vertexCount];
            _deltaTangent = new Vector3[_vertexCount];

            var blendshapeList = blendshapeNames.ToList();

            CreateBlendshape(EyeClosedLeft, eyeSmileLeft, HaiXT_EyeClosedInverse_SmileLeft, blendshapeList, mesh);
            CreateBlendshape(EyeClosedRight, eyeSmileRight, HaiXT_EyeClosedInverse_SmileRight, blendshapeList, mesh);

            smr.sharedMesh = mesh;
        }

        private void CreateBlendshape(string toBeInversed, string toBeAdded, string destination, List<string> blendshapeList, Mesh mesh)
        {
            var toBeInversedIndex = blendshapeList.IndexOf(toBeInversed);
            var toBeAddedIndex = blendshapeList.IndexOf(toBeAdded);
            mesh.GetBlendShapeFrameVertices(toBeInversedIndex, mesh.GetBlendShapeFrameCount(toBeInversedIndex) - 1, _storagePosition, _storageNormal, _storageTangent);
            mesh.GetBlendShapeFrameVertices(toBeAddedIndex, mesh.GetBlendShapeFrameCount(toBeAddedIndex) - 1, _deltaPosition, _deltaNormal, _deltaTangent);
            for (var i = 0; i < _vertexCount; i++)
            {
                _deltaPosition[i] -= _storagePosition[i];
                _deltaNormal[i] -= _storageNormal[i];
                _deltaTangent[i] -= _storageTangent[i];
            }
            mesh.AddBlendShapeFrame(destination, 100, _deltaPosition, _deltaNormal, _deltaTangent);
        }
    }
}