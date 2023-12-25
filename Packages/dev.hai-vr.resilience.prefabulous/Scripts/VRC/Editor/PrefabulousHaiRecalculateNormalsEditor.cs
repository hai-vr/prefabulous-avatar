using System;
using System.Collections.Generic;
using System.Linq;
using Prefabulous.Hai.Runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Prefabulous.VRC.Editor
{
    [CustomEditor(typeof(PrefabulousHaiRecalculateNormals))]
    public class PrefabulousHaiRecalculateNormalsEditor : UnityEditor.Editor
    {
        private static bool _doNotHideBodyMesh;
        private Dictionary<string, float> _animMaxvalsNullable;
        private GUIStyle _red;

        private void OnEnable()
        {
            _animMaxvalsNullable = null;
            
            var my = (PrefabulousHaiRecalculateNormals)target;
            var descriptor = my.transform.GetComponentInParent<VRCAvatarDescriptor>();
            if (descriptor == null) return;

            var blendShapePropertyPrefix = "blendShape.";
            var lengthOfBlendShapePrefix = blendShapePropertyPrefix.Length;
            var blendShapeValues = PrefabulousUtil.FindAllRelevantAnimationClips(descriptor)
                .SelectMany(clip =>
                {
                    return AnimationUtility.GetCurveBindings(clip)
                        .Where(binding => binding.type == typeof(SkinnedMeshRenderer))
                        .Where(binding => binding.propertyName.StartsWith(blendShapePropertyPrefix))
                        .Select(binding => new BlendShapeWeight(
                            binding.propertyName.Substring(lengthOfBlendShapePrefix),
                            AnimationUtility.GetEditorCurve(clip, binding).keys.Max(keyframe => keyframe.value)
                        ))
                        .Where(value => value.weight > 0f)
                        .ToArray();
                })
                .ToArray();
            
            var blendshapeToAnimMaxval = new Dictionary<string, float>();
            foreach (var blendShapeValue in blendShapeValues)
            {
                var blendShapeName = blendShapeValue.name;
                var weight = blendShapeValue.weight;
                if (blendshapeToAnimMaxval.TryGetValue(blendShapeName, out float existingWeight))
                {
                    if (weight > existingWeight)
                    {
                        blendshapeToAnimMaxval[blendShapeName] = weight;
                    }
                }
                else
                {
                    blendshapeToAnimMaxval[blendShapeName] = weight;
                }
            }

            _animMaxvalsNullable = blendshapeToAnimMaxval;
        }

        internal struct BlendShapeWeight
        {
            public BlendShapeWeight(string name, float weight)
            {
                this.name = name;
                this.weight = weight;
            }

            public string name;
            public float weight;
        }

        public override void OnInspectorGUI()
        {
            PrefabulousLocalization.Localization.DisplayLanguageSelector();
            
            _red = new GUIStyle(EditorStyles.textField);
            _red.normal.textColor = Color.red;
            
            var my = (PrefabulousHaiRecalculateNormals)target;

            if (my.blendShapes == null) my.blendShapes = new string[0];
            if (my.renderers == null) my.renderers = new SkinnedMeshRenderer[0];
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousHaiRecalculateNormals.blendShapes)), new GUIContent("BlendShapes"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousHaiRecalculateNormals.limitToSpecificMeshes)));
            if (my.limitToSpecificMeshes)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousHaiRecalculateNormals.renderers)));
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabulousHaiRecalculateNormals.eraseCustomSplitNormals)));
            if (GUILayout.Button("?", GUILayout.Width(50)))
            {
                Application.OpenURL("https://docs.hai-vr.dev/docs/products/prefabulous-avatar/hai-components/recalculate-normals#option-erase-custom-split-normals");
            }
            EditorGUILayout.EndHorizontal();
            
            if (my.eraseCustomSplitNormals)
            {
                EditorGUILayout.HelpBox("Erase Custom Split Normals is enabled.\nThis will cause a change in the recalculation algorithm. This change can cause worse results!\nPlease consult the documentation to learn more.", MessageType.Warning);
                if (GUILayout.Button("Open documentation"))
                {
                    Application.OpenURL("https://docs.hai-vr.dev/docs/products/prefabulous-avatar/hai-components/recalculate-normals#option-erase-custom-split-normals");
                }
            }
                    
            EditorGUILayout.Space();
            
            var smrs = FindSmrs(my);

            var affected = new List<SkinnedMeshRenderer>();
            var notAffected = new List<SkinnedMeshRenderer>();
            var smrToBlendshapes = new Dictionary<SkinnedMeshRenderer, List<string>>();
            var blendshapeToMaxval = new Dictionary<string, float>();
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;

                var shapes = GetAllBlendshapesOf(smr);
                if (!_doNotHideBodyMesh && shapes.Contains("vrc.v_aa")) continue;

                for (var i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    var blendShapeName = shapes[i];
                    var weight = smr.GetBlendShapeWeight(i);
                    if (blendshapeToMaxval.TryGetValue(blendShapeName, out float existingWeight))
                    {
                        if (weight > existingWeight)
                        {
                            blendshapeToMaxval[blendShapeName] = weight;
                        }
                    }
                    else
                    {
                        blendshapeToMaxval[blendShapeName] = weight;
                    }
                }

                smrToBlendshapes[smr] = shapes;
                var isAffected = my.blendShapes.Any(s => shapes.Contains(s));
                if (isAffected)
                {
                    affected.Add(smr);
                }
                else
                {
                    notAffected.Add(smr);
                }
            }

            if (_animMaxvalsNullable != null)
            {
                foreach (var animMaxvals in _animMaxvalsNullable)
                {
                    if (blendshapeToMaxval.ContainsKey(animMaxvals.Key))
                    {
                        if (animMaxvals.Value > blendshapeToMaxval[animMaxvals.Key])
                        {
                            blendshapeToMaxval[animMaxvals.Key] = animMaxvals.Value;
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            foreach (var blendShape in my.blendShapes)
            {
                EditorGUILayout.LabelField(blendShape, EditorStyles.boldLabel);
                var foundAny = false;
                foreach (var smrToBlendshape in smrToBlendshapes)
                {
                    if (smrToBlendshape.Value.Contains(blendShape))
                    {
                        foundAny = true;
                        EditorGUILayout.ObjectField(smrToBlendshape.Key, typeof(SkinnedMeshRenderer));
                    }
                }

                if (!foundAny)
                {
                    EditorGUILayout.LabelField("(No meshes found)", EditorStyles.label);
                }
            }

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Add BlendShapes", EditorStyles.boldLabel);

                _doNotHideBodyMesh = EditorGUILayout.Toggle(new GUIContent("Show face blendShapes"), _doNotHideBodyMesh);

                var weightedBlendshapes = blendshapeToMaxval
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key)
                    .ToArray();

                foreach (var blendshapeToWeight in weightedBlendshapes)
                {
                    var blendShapeName = blendshapeToWeight.Key;

                    EditorGUILayout.BeginHorizontal();
                    var bslower = blendShapeName.ToLowerInvariant();
                    if (bslower.StartsWith("kisekae_")
                        || bslower.StartsWith("shrink_")
                        || bslower.StartsWith("hidemesh_")
                        )
                    {
                        EditorGUILayout.TextField(blendShapeName, _red);
                    }
                    else
                    {
                        EditorGUILayout.TextField(blendShapeName);
                    }
                    EditorGUILayout.TextField($"{(int)Mathf.Floor(blendshapeToWeight.Value)}", GUILayout.Width(50));
                    EditorGUI.BeginDisabledGroup(my.blendShapes.Contains(blendShapeName));
                    if (GUILayout.Button("+ Add", GUILayout.Width(50)))
                    {
                        var bsp = serializedObject.FindProperty(nameof(PrefabulousHaiRecalculateNormals.blendShapes));
                        bsp.arraySize += 1;
                        bsp.GetArrayElementAtIndex(bsp.arraySize - 1).stringValue = blendShapeName;
                    }

                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static SkinnedMeshRenderer[] FindSmrs(PrefabulousHaiRecalculateNormals my)
        {
            if (my.limitToSpecificMeshes && my.renderers != null) return my.renderers;
            
            var animators = my.transform.GetComponentsInParent<Animator>(true);
            if (animators.Length == 0) return Array.Empty<SkinnedMeshRenderer>();
            
            var lastAnimator = animators.Last();
            return lastAnimator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        private List<string> GetAllBlendshapesOf(SkinnedMeshRenderer smr)
        {
            var sharedMesh = smr.sharedMesh;

            return new List<string>(Enumerable.Range(0, sharedMesh.blendShapeCount)
                .Select(i => sharedMesh.GetBlendShapeName(i))
                .ToList());
        }
    }
}