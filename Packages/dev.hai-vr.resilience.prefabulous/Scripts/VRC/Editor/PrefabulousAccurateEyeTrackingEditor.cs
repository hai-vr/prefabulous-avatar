using Prefabulous.VRC.Runtime;
using UnityEditor;

namespace Prefabulous.VRC.Editor
{
    [CustomEditor(typeof(PrefabulousAccurateEyeTracking))]
    public class PrefabulousAccurateEyeTrackingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var my = (PrefabulousAccurateEyeTracking)target;

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousAccurateEyeTracking.leftEyeYaw)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousAccurateEyeTracking.leftEyePitch)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousAccurateEyeTracking.rightEyeYaw)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousAccurateEyeTracking.rightEyePitch)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousAccurateEyeTracking.vendor)));
            
            if (my.vendor == PrefabulousAccurateEyeTrackingAnimatorVendor.Custom)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousAccurateEyeTracking.leftXParam)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousAccurateEyeTracking.rightXParam)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousAccurateEyeTracking.leftYParam)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousAccurateEyeTracking.rightYParam)));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}