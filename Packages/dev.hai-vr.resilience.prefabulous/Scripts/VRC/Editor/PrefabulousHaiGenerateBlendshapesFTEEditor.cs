using System;
using System.Collections.Generic;
using System.Linq;
using Prefabulous.Hai.Runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Prefabulous.VRC.Editor
{
    [CustomEditor(typeof(PrefabulousHaiGenerateBlendshapesFTE))]
    public class PrefabulousHaiGenerateBlendshapesFTEEditor : UnityEditor.Editor
    {
        private const string CrossSymbol = "×";
        private static bool _doNotHideBodyMesh;
        private Dictionary<string, float> _animMaxvalsNullable;

        public override void OnInspectorGUI()
        {
            var my = (PrefabulousHaiGenerateBlendshapesFTE)target;

            var hasBody = false;
            var hasFaceTrackingBlendshapes = false;
            var hasExtensionsAnimator = false;
            var descriptor = my.GetComponentInParent<VRCAvatarDescriptor>();
            var blendshapeNames = Array.Empty<string>();
            if (descriptor != null)
            {
                var body = descriptor.transform.Find(PrefabulousHaiGenerateBlendshapesFTEPlugin.Body);
                if (body != null)
                {
                    var smr = body.GetComponent<SkinnedMeshRenderer>();
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(new GUIContent(PrefabulousLocalizationPhrase.FaceMeshLabel), smr, typeof(SkinnedMeshRenderer));
                    EditorGUI.EndDisabledGroup();
                    hasBody = true;

                    blendshapeNames = PrefabulousUtil.GetAllBlendshapeNames(smr);
                    if (blendshapeNames.Contains(PrefabulousHaiGenerateBlendshapesFTEPlugin.EyeClosedLeft) && blendshapeNames.Contains(PrefabulousHaiGenerateBlendshapesFTEPlugin.EyeClosedRight))
                    {
                        hasFaceTrackingBlendshapes = true;
                    }
                }

                hasExtensionsAnimator = descriptor.transform.GetComponentInChildren<PrefabulousHaiFaceTrackingExtensions>(true) != null;
            }

            if (!hasBody)
            {
                EditorGUILayout.HelpBox(PrefabulousLocalizationPhrase.MsgMissingBody, MessageType.Error);
            }

            if (hasBody && !hasExtensionsAnimator)
            {
                EditorGUILayout.HelpBox(PrefabulousLocalizationPhrase.MsgMissingComponent, MessageType.Warning);
                if (GUILayout.Button(PrefabulousLocalizationPhrase.AddComponentLabel))
                {
                    Undo.AddComponent<PrefabulousHaiFaceTrackingExtensions>(my.gameObject);
                }
            }
            
            EditorGUILayout.Space();

            GUILayout.BeginVertical("GroupBox");
            EditorGUILayout.LabelField(PrefabulousHaiGenerateBlendshapesFTEPlugin.HaiXT_EyeClosedInverse_Smile, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(PrefabulousLocalizationPhrase.MsgExplainHaiXT_EyeClosedInverse_Smile, MessageType.None);
            if (hasBody && !hasFaceTrackingBlendshapes)
            {
                EditorGUILayout.HelpBox(PrefabulousLocalizationPhrase.MsgMissingPreconditionForHaiXT_EyeClosedInverse_Smile, MessageType.Error);
            }
            
            EditorGUILayout.BeginHorizontal();
            var eyeLeft = serializedObject.FindProperty(nameof(PrefabulousHaiGenerateBlendshapesFTE.EyeClosedInverse_Smile_EyeLeft));
            EditorGUILayout.PropertyField(eyeLeft, new GUIContent(PrefabulousLocalizationPhrase.LeftEyeClosedSmilingLabel));
            if (GUILayout.Button(CrossSymbol, GUILayout.Width(25)))
            {
                eyeLeft.stringValue = "";
            }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(my.EyeClosedInverse_Smile_EyeLeft) && !blendshapeNames.Contains(my.EyeClosedInverse_Smile_EyeLeft))
            {
                EditorGUILayout.HelpBox(PrefabulousLocalizationPhrase.MsgNoSuchBlendshapeName, MessageType.Error);
            }
            
            EditorGUILayout.BeginHorizontal();
            var eyeRight = serializedObject.FindProperty(nameof(PrefabulousHaiGenerateBlendshapesFTE.EyeClosedInverse_Smile_EyeRight));
            EditorGUILayout.PropertyField(eyeRight, new GUIContent(PrefabulousLocalizationPhrase.RightEyeClosedSmilingLabel));
            if (GUILayout.Button(CrossSymbol, GUILayout.Width(25)))
            {
                eyeRight.stringValue = "";
            }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(my.EyeClosedInverse_Smile_EyeRight) && !blendshapeNames.Contains(my.EyeClosedInverse_Smile_EyeRight))
            {
                EditorGUILayout.HelpBox(PrefabulousLocalizationPhrase.MsgNoSuchBlendshapeName, MessageType.Error);
            }

            if (string.IsNullOrEmpty(my.EyeClosedInverse_Smile_EyeLeft) || string.IsNullOrEmpty(my.EyeClosedInverse_Smile_EyeRight))
            {
                foreach (var blendshapeName in blendshapeNames
                             .Where(s => !s.ToLowerInvariant().StartsWith("vrc.")))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.TextField(blendshapeName);
                    EditorGUI.BeginDisabledGroup(blendshapeName == my.EyeClosedInverse_Smile_EyeLeft);
                    if (GUILayout.Button(PrefabulousLocalizationPhrase.LeftLabel, GUILayout.Width(50)))
                    {
                        eyeLeft.stringValue = blendshapeName;
                    }
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUI.BeginDisabledGroup(blendshapeName == my.EyeClosedInverse_Smile_EyeRight);
                    if (GUILayout.Button(PrefabulousLocalizationPhrase.RightLabel, GUILayout.Width(50)))
                    {
                        eyeRight.stringValue = blendshapeName;
                    }
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            GUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}