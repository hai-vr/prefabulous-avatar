using System.Collections.Generic;
using Prefabulous.Universal.Common.Runtime;
using UnityEditor;
using VRC.SDK3.Avatars.Components;

namespace Prefabulous.VRC.Editor
{
    [CustomEditor(typeof(PrefabulousChangeAvatarScale))]
    public class PrefabulousChangeAvatarScaleEditor : UnityEditor.Editor
    {
        private Dictionary<string, float> _animMaxvalsNullable;

        public override void OnInspectorGUI()
        {
            var my = (PrefabulousChangeAvatarScale)target;

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousChangeAvatarScale.customSourceSize)));
            if (my.customSourceSize)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousChangeAvatarScale.sourceSizeInMeters)));
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                var descriptor = my.transform.GetComponentInParent<VRCAvatarDescriptor>();
                if (descriptor != null)
                {
                    EditorGUILayout.TextField("Source Size In Meters", $"{descriptor.ViewPosition.y}");
                }
                else
                {
                    EditorGUILayout.TextField("Source Size In Meters", "?");
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousChangeAvatarScale.desiredSizeInMeters)));

            serializedObject.ApplyModifiedProperties();
        }
    }
}