using nadena.dev.modular_avatar.core;
using Prefabulous.Hai.Runtime;
using Prefabulous.VRC.Runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Prefabulous.VRC.Editor
{
    [CustomEditor(typeof(PrefabulousBlankExpressions))] [CanEditMultipleObjects] public class PrefabulousBlankExpressionsEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousBlankFXAnimator))] [CanEditMultipleObjects] public class PrefabulousBlankFXAnimatorEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousBlankGestureAnimator))] [CanEditMultipleObjects] public class PrefabulousBlankGestureAnimatorEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousEditAllMeshAnchorOverride))] [CanEditMultipleObjects] public class PrefabulousEditMeshAnchorOverrideEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousEditAllMeshBounds))] [CanEditMultipleObjects] public class PrefabulousEditMeshBoundsEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousHaiAccurateEyeTracking))] [CanEditMultipleObjects] public class PrefabulousHaiAccurateEyeTrackingEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousHaiFaceTrackingExtensions))] [CanEditMultipleObjects] public class PrefabulousHaiFaceTrackingExtensionsEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousImportExpressionParameters))] [CanEditMultipleObjects] public class PrefabulousImportExpressionParametersEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousReplaceActionAnimator))] [CanEditMultipleObjects] public class PrefabulousReplaceActionAnimatorEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousReplaceLocomotionAnimator))] [CanEditMultipleObjects] public class PrefabulousReplaceLocomotionAnimatorEditor : PrefabulousEditor { }

    [CustomEditor(typeof(PrefabulousHaiLockLocomotionMenuItem))]
    [CanEditMultipleObjects]
    public class PrefabulousHaiLockLocomotionEditor : PrefabulousEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var my = (PrefabulousHaiLockLocomotionMenuItem)target;
            if (my.transform.GetComponent<ModularAvatarMenuItem>() == null)
            {
                my.gameObject.AddComponent<ModularAvatarMenuItem>();
            }
            
            var menu = my.transform.GetComponent<ModularAvatarMenuItem>();
            menu.hideFlags = HideFlags.NotEditable;
            menu.Control.icon = my.icon;
            menu.Control.parameter.name = PrefabulousHaiLockLocomotionMenuItemPlugin.ParameterName;
            menu.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            menu.MenuSource = SubmenuSource.Children;
        }
    }
    
    
    public class PrefabulousEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // using (new UnityEditor.LocalizationGroup(target))
            // {
                EditorGUI.BeginChangeCheck();
                serializedObject.UpdateIfRequiredOrScript();
                var iterator = serializedObject.GetIterator();
                var count = 0;
                for (var enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
                {
                    if (iterator.propertyPath != "m_Script")
                    {
                        count++;
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                }

                if (count == 0)
                {
                    EditorGUILayout.LabelField("(This component has no properties)");
                }
                serializedObject.ApplyModifiedProperties();
                EditorGUI.EndChangeCheck();
            // }
        }
    }
}