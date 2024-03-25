using System.Collections.Generic;
using System.Linq;
using Prefabulous.Hai.Runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VRC.Editor
{
    [CustomEditor(typeof(PrefabulousHaiReplaceTextures))]
    public class PrefabulousHaiReplaceTexturesEditor : UnityEditor.Editor
    {
        private Texture[] _foundTextures;
        private Texture2D _iconBackground;

        private void OnEnable()
        {
            var my = (PrefabulousHaiReplaceTextures)target;
            
            var descriptor = my.transform.GetComponentInParent<VRCAvatarDescriptor>();
            if (descriptor == null) return;

            _foundTextures = ExcludeEditorOnly(descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true)).SelectMany(renderer => renderer.sharedMaterials)
                .Concat(ExcludeEditorOnly(descriptor.GetComponentsInChildren<MeshRenderer>(true)).SelectMany(renderer => renderer.sharedMaterials))
                .Concat(ExcludeEditorOnly(descriptor.GetComponentsInChildren<LineRenderer>(true)).SelectMany(renderer => renderer.sharedMaterials))
                .Concat(ExcludeEditorOnly(descriptor.GetComponentsInChildren<ParticleSystemRenderer>(true)).SelectMany(renderer => renderer.sharedMaterials))
                .Concat(ExcludeEditorOnly(descriptor.GetComponentsInChildren<ParticleSystemRenderer>(true)).Select(renderer => renderer.trailMaterial))
                .Distinct()
                .Where(material => material != null)
                .SelectMany(material => material.GetTexturePropertyNameIDs().Select(material.GetTexture))
                .Where(texture => texture != null)
                .Distinct()
                .ToArray();
        }

        private T[] ExcludeEditorOnly<T>(T[] comps) where T : Component
        {
            return comps.Where(comp => !IsInEditorOnly(comp.transform)).ToArray();
        }

        private bool IsInEditorOnly(Transform t)
        {
            if (t.CompareTag("EditorOnly")) return true;
            var parent = t.parent;
            return parent != null && IsInEditorOnly(parent);
        }

        public override void OnInspectorGUI()
        {
            if (_iconBackground == null)
            {
                _iconBackground = new Texture2D(1, 1);
                _iconBackground.SetPixel(0, 0, new Color(0.01f, 0.2f, 0.2f));
                _iconBackground.Apply();
            }
            var my = (PrefabulousHaiReplaceTextures)target;
            var sources = new HashSet<Texture2D>(my.replacements
                .Select(substitution => substitution.source)
                .Where(texture2D => texture2D != null));

            var replacementsProperty = serializedObject.FindProperty(nameof(PrefabulousHaiReplaceTextures.replacements));
            EditorGUILayout.PropertyField(replacementsProperty, new GUIContent("Replacements"));
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousHaiReplaceTextures.executeInPlayMode)), new GUIContent("(DANGER) Execute in Play Mode"));
            EditorGUILayout.HelpBox(@"If you choose to execute Replace Textures in Play Mode, it can be tremendously confusing for your workflow as you will no longer be able to edit the materials of your avatar while in Play Mode.

For this reason, it is NOT recommended to execute this component in Play Mode. Replace Textures will always be executed when building your avatar for upload, or when baking your avatar.", MessageType.Warning);

            
            foreach (var foundTexture in _foundTextures)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Box(foundTexture, new GUIStyle("box")
                    {
                        normal = new GUIStyleState { background = _iconBackground }
                    },
                    GUILayout.Width(128), GUILayout.Height(128)
                );
                EditorGUILayout.BeginVertical();
                
                var hasTexture = sources.Contains(foundTexture);
                
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(foundTexture, typeof(Texture));
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.LabelField($"{foundTexture.width} \u00d7 {foundTexture.height}");
                
                EditorGUI.BeginDisabledGroup(hasTexture);
                if (GUILayout.Button("+ Add", GUILayout.Width(50)))
                {
                    replacementsProperty.arraySize += 1;
                    var that = replacementsProperty.GetArrayElementAtIndex(replacementsProperty.arraySize - 1);
                    that.FindPropertyRelative(nameof(PrefabulousTextureSubstitution.source)).objectReferenceValue = foundTexture;
                    that.FindPropertyRelative(nameof(PrefabulousTextureSubstitution.target)).objectReferenceValue = null;
                }
                EditorGUI.EndDisabledGroup();
                if (hasTexture)
                {
                    var index = SourceIndexOf(my.replacements, foundTexture);
                    if (replacementsProperty.arraySize <= index) continue; // Workaround
                    
                    var field = replacementsProperty
                        .GetArrayElementAtIndex(index)
                        .FindPropertyRelative(nameof(PrefabulousTextureSubstitution.target));
                    EditorGUILayout.PropertyField(field, new GUIContent("Replace with"));
                    
                    var replaceWith = (Texture2D)field.objectReferenceValue;
                    if (replaceWith != null)
                    {
                        EditorGUILayout.LabelField($"{replaceWith.width} \u00d7 {replaceWith.height}");
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        private int SourceIndexOf(PrefabulousTextureSubstitution[] myReplacements, Texture foundTexture)
        {
            // Bruh
            for (var index = 0; index < myReplacements.Length; index++)
            {
                var replacement = myReplacements[index];
                if (replacement.source == foundTexture)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}