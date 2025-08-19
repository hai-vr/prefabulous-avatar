﻿using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.VRC.Runtime
{
    [AddComponentMenu("Prefabulous/PA-VRC Lock Locomotion Menu Item")]
    [HelpURL("https://docs.hai-vr.dev/redirect/components/PrefabulousLockLocomotionMenuItem")]
    public class PrefabulousLockLocomotionMenuItem : MonoBehaviour, IEditorOnly
    {
        public Texture2D icon;

        private void OnDestroy()
        {
            if (Application.isPlaying) return;
            
            var menu = GetComponent<ModularAvatarMenuItem>();
            if (menu != null && menu.hideFlags == HideFlags.NotEditable)
            {
                menu.hideFlags = HideFlags.None;
                DestroyImmediate(menu);
            }
        }
    }
}
