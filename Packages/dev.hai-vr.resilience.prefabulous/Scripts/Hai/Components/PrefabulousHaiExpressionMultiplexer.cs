using System;
using UnityEngine;
using VRC.SDKBase;

namespace Prefabulous.Hai.Runtime
{
    [AddComponentMenu("Prefabulous Avatar/PA-H Expression Multiplexer")]
    public class PrefabulousHaiExpressionMultiplexer : MonoBehaviour, IEditorOnly
    {
        public PrefabulousHaiExpressionMultiplexerParameter[] parameters;
        public bool useStrategyEvenWhenUnderLimit;
    }

    [Serializable]
    public struct PrefabulousHaiExpressionMultiplexerParameter
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