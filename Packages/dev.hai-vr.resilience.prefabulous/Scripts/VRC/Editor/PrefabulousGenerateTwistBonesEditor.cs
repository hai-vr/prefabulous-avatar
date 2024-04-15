using System.Collections.Generic;
using System.Linq;
using Prefabulous.Universal.Common.Runtime;
using UnityEditor;
using UnityEngine;

namespace Prefabulous.Universal.Shared.Editor
{
    [CustomEditor(typeof(PrefabulousGenerateTwistBones))]
    public class PrefabulousGenerateTwistBonesEditor : UnityEditor.Editor
    {
        private const string OptionsLabel = "Options";
        private const string BasicTwistBonesLabel = "Basic twist bones";
        private const string CustomTwistBoneLabel = "Custom Twist Bone";
        private const string AlphaExperimentalCompatibilityOptionsLabel = "(Alpha: Experimental Compatibility Options)";

        private static bool _doNotHideBodyMesh;
        private GUIStyle _red;

        public override void OnInspectorGUI()
        {
            if (_red == null)
            {
                _red = new GUIStyle(EditorStyles.textField);
                _red.normal.textColor = Color.red;
            }

            var my = (PrefabulousGenerateTwistBones)target;
            
            EditorGUILayout.LabelField(BasicTwistBonesLabel, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.leftElbowJointLowerArm)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.rightElbowJointLowerArm)));
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField(OptionsLabel, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.weightDistribution)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.excludeBraceletsAndWristwatchesBlendshapes)));
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField(CustomTwistBoneLabel, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.useCustom)));
            if (my.useCustom)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.upperUpSuggestion)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.lowerUpSuggestion)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.upper)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.lower)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.tip)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.isMainArmature)));
            }
            
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(AlphaExperimentalCompatibilityOptionsLabel, EditorStyles.boldLabel);
            if (GUILayout.Button("?", GUILayout.Width(50)))
            {
                Application.OpenURL("http://localhost:3000/docs/products/prefabulous/universal/generate-twist-bones");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.BeginDisabledGroup(my.generateInOptimizingPhase);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.generateBeforeModularAvatarMergeArmature)));
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(my.generateBeforeModularAvatarMergeArmature);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousGenerateTwistBones.generateInOptimizingPhase)));
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }
    }
}