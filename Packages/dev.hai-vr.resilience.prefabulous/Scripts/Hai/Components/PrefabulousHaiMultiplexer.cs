using System;
using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.Hai.Runtime
{
    [AddComponentMenu("Prefabulous Avatar/PA-H Multiplexer")]
    public class PrefabulousHaiMultiplexer : MonoBehaviour, IEditorOnly
    {
        public PrefabulousHaiMultiplexerParameter[] parameters;
        public bool useEvenWhenUnderLimit;
    }

    [Serializable]
    public struct PrefabulousHaiMultiplexerParameter
    {
        public string name;
        public MultiplexerStrategy strategy;
    }

    public enum MultiplexerStrategy
    {
         Default,
         LowUpdateRate,
         RealTime,
         // Quantize,
         // HalfTime,
    }
}