using nadena.dev.modular_avatar.core;
using Prefabulous.Native.Shared.Editor;
using Prefabulous.VRC.Runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Prefabulous.VRC.Editor
{
    [CustomEditor(typeof(PrefabulousFaceTrackingExtensions))] [CanEditMultipleObjects] public class PrefabulousHaiFaceTrackingExtensionsEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousBlankExpressions))] [CanEditMultipleObjects] public class PrefabulousBlankExpressionsEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousBlankFXAnimator))] [CanEditMultipleObjects] public class PrefabulousBlankFXAnimatorEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousBlankGestureAnimator))] [CanEditMultipleObjects] public class PrefabulousBlankGestureAnimatorEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousImportExpressionParameters))] [CanEditMultipleObjects] public class PrefabulousImportExpressionParametersEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousReplaceActionAnimator))] [CanEditMultipleObjects] public class PrefabulousReplaceActionAnimatorEditor : PrefabulousEditor { }
    [CustomEditor(typeof(PrefabulousReplaceLocomotionAnimator))] [CanEditMultipleObjects] public class PrefabulousReplaceLocomotionAnimatorEditor : PrefabulousEditor { }

    [CustomEditor(typeof(PrefabulousLockLocomotionMenuItem))]
    [CanEditMultipleObjects]
    public class PrefabulousHaiLockLocomotionEditor : PrefabulousEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var my = (PrefabulousLockLocomotionMenuItem)target;
            if (my.transform.GetComponent<ModularAvatarMenuItem>() == null)
            {
                my.gameObject.AddComponent<ModularAvatarMenuItem>();
            }
            
            var menu = my.transform.GetComponent<ModularAvatarMenuItem>();
            menu.hideFlags = HideFlags.NotEditable;
            menu.Control.icon = my.icon;
            menu.Control.parameter.name = PrefabulousLockLocomotionMenuItemPlugin.ParameterName;
            menu.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            menu.MenuSource = SubmenuSource.Children;
        }
    }
}