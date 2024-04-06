using UnityEngine;
using IPrefabulousEditorOnly = VRC.SDKBase.IEditorOnly;

namespace Prefabulous.Universal.Common.Runtime
{
    [AddComponentMenu("Prefabulous/PA Generate Twist Bones (Alpha)")]
    public class PrefabulousGenerateTwistBones : MonoBehaviour, IPrefabulousEditorOnly
    {
        public string[] excludeBraceletsAndWristwatchesBlendshapes;
        
        public bool leftElbowJointLowerArm = true;
        public bool rightElbowJointLowerArm = true;
        
        public bool useCustom;
        public Vector3 upperUpSuggestion = Vector3.forward;
        public Vector3 lowerUpSuggestion = Vector3.forward;
        public Transform upper;
        public Transform lower;
        public Transform tip;
        public bool isMainArmature;
        public AnimationCurve weightDistribution = AnimationCurve.Linear(0, 0, 1f, 1f);

        public bool generateInOptimizingPhase; // Priority
        public bool generateBeforeModularAvatarMergeArmature;
    }
}