using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Prefabulous.VRC.Runtime;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Prefabulous.VRC.Editor
{
    [ParameterProviderFor(typeof(PrefabulousImportExpressionParameters))]
    public class PrefabulousImportExpressionParametersParameterProvider : IParameterProvider
    {
        private readonly PrefabulousImportExpressionParameters _importExpressionParameters;

        public PrefabulousImportExpressionParametersParameterProvider(PrefabulousImportExpressionParameters importExpressionParameters)
        {
            _importExpressionParameters = importExpressionParameters;
            
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context = null)
        {
            if (_importExpressionParameters.parameters == null) return Enumerable.Empty<ProvidedParameter>();
            
            return _importExpressionParameters.parameters.parameters.Select(parameter => new ProvidedParameter(parameter.name,
                ParameterNamespace.Animator,
                _importExpressionParameters,
                PrefabulousImportExpressionParametersPlugin.Instance,
                AsParamType(parameter.valueType)
            )
            {
                WantSynced = parameter.networkSynced
            }).ToList();
        }

        private AnimatorControllerParameterType AsParamType(VRCExpressionParameters.ValueType parameterValueType)
        {
            switch (parameterValueType)
            {
                case VRCExpressionParameters.ValueType.Int:
                    return AnimatorControllerParameterType.Int;
                case VRCExpressionParameters.ValueType.Float:
                    return AnimatorControllerParameterType.Float;
                case VRCExpressionParameters.ValueType.Bool:
                    return AnimatorControllerParameterType.Bool;
                default:
                    throw new ArgumentOutOfRangeException(nameof(parameterValueType), parameterValueType, null);
            }
        }
    }
}