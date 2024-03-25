using System;
using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.Hai.Runtime
{
    [AddComponentMenu("Prefabulous Avatar/PA-H Replace Textures")]
    public class PrefabulousHaiReplaceTextures : MonoBehaviour, IEditorOnly
    {
        public bool executeInPlayMode = false;
        public PrefabulousTextureSubstitution[] replacements;
    }

    [Serializable]
    public struct PrefabulousTextureSubstitution
    {
        public Texture2D source;
        public Texture2D target;
    }
}