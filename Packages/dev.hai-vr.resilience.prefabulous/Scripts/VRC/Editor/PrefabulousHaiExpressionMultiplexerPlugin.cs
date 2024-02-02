using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;
using nadena.dev.ndmf;
using Prefabulous.Hai.Runtime;
using Prefabulous.VRC.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(PrefabulousHaiExpressionMultiplexerPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousHaiExpressionMultiplexerPlugin : Plugin<PrefabulousHaiExpressionMultiplexerPlugin>
    {
        // VRC's update delay is supposedly 0.1 of a second (10 updates per second).
        // However, if the delay is set to exactly 0.1, there is a risk that the parameter value serialization will be performed in such a way that one of the packets will be skipped.
        // I'd rather have a duplicate than a skip, so I'll increase the delay here. 
        private const float UpdateDelaySeconds = 0.12f;

        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing)
                .Run("Expression Multiplexer", context =>
                {
                    var prefabulousComps = context.AvatarRootTransform.GetComponentsInChildren<PrefabulousHaiExpressionMultiplexer>(true);
                    if (prefabulousComps.Length == 0) return;

                    var forceStrategy = prefabulousComps.Any(multiplexer => multiplexer.useStrategyEvenWhenUnderLimit);

                    var expressionParameters = Object.Instantiate(context.AvatarDescriptor.expressionParameters);
                    context.AvatarDescriptor.expressionParameters = expressionParameters;
                    
                    var originalCost = expressionParameters.CalcTotalCost();
                    if (originalCost < MaxParamCost())
                    {
                        var msg = $"Total parameter cost ({originalCost}) is lower than maximum ({MaxParamCost()}.";
                        if (forceStrategy)
                        {
                            Debug.Log($"(ExpressionMultiplexer) {msg}. No optimization is needed, however, at least one component has useStrategyEvenWhenUnderLimit, so we will process it regardless.");
                        }
                        else
                        {
                            Debug.Log($"(ExpressionMultiplexer) {msg}. No optimization needed, skipping.");
                            return; // Don't process any further
                        }
                    }
                    else
                    {
                        Debug.Log($"(ExpressionMultiplexer) The current total cost is {originalCost}.");
                    }

                    var existingParameters = new HashSet<string>(expressionParameters.parameters
                        .Where(parameter => parameter != null)
                        .Where(parameter => parameter.networkSynced)
                        .Select(parameter => parameter.name)
                        .ToArray());

                    var provenNameToStrategy = prefabulousComps
                        .SelectMany(multiplexer => multiplexer.parameters)
                        .Where(parameter => existingParameters.Contains(parameter.name))
                        .GroupBy(parameter => parameter.name)
                        .ToDictionary(grouping => grouping.Key, grouping => grouping.FirstOrDefault());
                    
                    var usedStrategies = provenNameToStrategy.Values
                        .Select(parameter => parameter.strategy)
                        .Distinct()
                        .ToArray();

                    if (!usedStrategies.Contains(MultiplexerStrategy.LowUpdateRate)) return;

                    var lowUpdateRateParams = provenNameToStrategy
                        .Where(pair => pair.Value.strategy == MultiplexerStrategy.LowUpdateRate)
                        .ToDictionary(pair => pair.Key, pair => pair.Value);

                    foreach (var parameter in expressionParameters.parameters)
                    {
                        if (lowUpdateRateParams.ContainsKey(parameter.name))
                        {
                            parameter.networkSynced = false;
                        }
                    }
                    
                    Debug.Log($"(ExpressionMultiplexer) We have a total of {lowUpdateRateParams.Count} LowUpdateRate parameters to send.");
                    Debug.Log($"(ExpressionMultiplexer) The expression parameter currently has {expressionParameters.parameters.Length} parameters.");

                    var potentialCost = expressionParameters.CalcTotalCost();
                    var savingsWithoutAccountingForMultiplexer = originalCost - potentialCost;
                    Debug.Log($"(ExpressionMultiplexer) Without accounting for the multiplexer cost, we're going from {originalCost} bits down to {potentialCost} bits, giving us savings of {savingsWithoutAccountingForMultiplexer} bits.");
                    
                    var leeway = MaxParamCost() - potentialCost;
                    Debug.Log($"(ExpressionMultiplexer) The maximum is {MaxParamCost()} bits, so {MaxParamCost()} - {potentialCost} = {leeway} bits of leeway to work with.");

                    // TODO: Turn these low update rate params into packets
                    var fxAnimator = (AnimatorController)context.AvatarDescriptor.baseAnimationLayers
                        .First(layer => layer.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;

                    var packettization = TryPackettize(lowUpdateRateParams, expressionParameters.parameters, fxAnimator.parameters, leeway);

                    var newParams = expressionParameters.parameters.ToList();
                    newParams.Add(new VRCExpressionParameters.Parameter
                    {
                        name = MultiplexerValue0(),
                        networkSynced = true,
                        valueType = VRCExpressionParameters.ValueType.Int,
                        saved = false,
                        defaultValue = 0f
                    });

                    var numberOfBitsRequiredToEncodeAddress = packettization.NumberOfBitsRequiredToEncodeAddress();
                    for (var i = 0; i < numberOfBitsRequiredToEncodeAddress; i++)
                    {
                        newParams.Add(new VRCExpressionParameters.Parameter
                        {
                            name = MultiplexerAddressForBit(i),
                            networkSynced = true,
                            valueType = VRCExpressionParameters.ValueType.Bool,
                            saved = false,
                            defaultValue = 0f
                        });
                    }
                    
                    expressionParameters.parameters = newParams.ToArray();
                    
                    // TODO: Group up lowUpdateRateParams by type to send in bulks
                    // TODO: Handle more than one address bit
                    // TODO: Handle having more than 8 bits of bandwidth for the value part
                    // TODO: Handle having less than 8 bits of bandwidth for the value part
                    // TODO: Handle having a non-multiple of 8 bits of bandwidth for the value part
                    CreateFxLayer(context, packettization, fxAnimator);
                });
        }

        private Packettization TryPackettize(
            Dictionary<string, PrefabulousHaiExpressionMultiplexerParameter> lowUpdateRateParams,
            VRCExpressionParameters.Parameter[] expressionParameters,
            AnimatorControllerParameter[] animatorParameters,
            int leeway
        )
        {
            // We're free to consume all remaining bits available. Therefore, if we can make sequential sync overall faster by increasing the packet size, we should try.
            // In addition, increasing the packet size could reduce the number of addresses needed, and thus, the number of bits needed for the address.
            
            var paramUnits = lowUpdateRateParams
                .Select(pair =>
                {
                    var name = pair.Value.name;
                    var isDeclaredInAnimator = animatorParameters.Any(parameter => parameter.name == name);
                    var expressionParameterDeclaredType = expressionParameters.First(parameter => parameter.name == name).valueType;
                    return new ParamUnit
                    {
                        name = name,
                        expressionParameterDeclaredType = expressionParameterDeclaredType,
                        isDeclaredInAnimator = isDeclaredInAnimator,
                        animatorDeclaredType = isDeclaredInAnimator ? animatorParameters.First(parameter => parameter.name == name).type : AsAnimatorType(expressionParameterDeclaredType)
                    };
                })
                .ToArray();
            
            // FIXME: Naive packettization
            var packetUnits = paramUnits
                .Select(paramUnit => new PacketUnit
                {
                    parameters = new[] { paramUnit }
                })
                .ToArray();

            return new Packettization
            {
                packets = packetUnits
            };
        }

        private AnimatorControllerParameterType AsAnimatorType(VRCExpressionParameters.ValueType expressionParameterDeclaredType)
        {
            switch (expressionParameterDeclaredType)
            {
                case VRCExpressionParameters.ValueType.Int:
                    return AnimatorControllerParameterType.Int;
                case VRCExpressionParameters.ValueType.Float:
                    return AnimatorControllerParameterType.Float;
                case VRCExpressionParameters.ValueType.Bool:
                    return AnimatorControllerParameterType.Bool;
                default:
                    throw new ArgumentOutOfRangeException(nameof(expressionParameterDeclaredType), expressionParameterDeclaredType, null);
            }
        }

        private static int MaxParamCost()
        {
            return VRCExpressionParameters.MAX_PARAMETER_COST;
        }

        private void CreateFxLayer(BuildContext ctx, Packettization packettization, AnimatorController ctrl)
        {
            var aac = AacV1.Create(new AacConfiguration
            {
                SystemName = GetType().Name,
                AnimatorRoot = ctx.AvatarRootTransform,
                DefaultValueRoot = ctx.AvatarRootTransform,
                AssetKey = GUID.Generate().ToString(),
                AssetContainer = ctx.AssetContainer,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
                DefaultsProvider = new AacDefaultsProvider(true)
            });

            // We are at the optimization phase, so we can't use the usual non-destructive methods.
            // FIXME: Is there a risk that this controller is an original?

            var fx = aac.CreateMainArbitraryControllerLayer(ctrl);

            var focus = fx.IntParameter(MultiplexerFocus());
            var init = fx.NewState("Init")
                .Drives(focus, 1);

            var tempoAnimation = aac.DummyClipLasting(UpdateDelaySeconds, AacFlUnit.Seconds);

            var sender = fx.NewState("Sender")
                .Shift(init, -2, 1)
                .WithAnimation(tempoAnimation);
            var receiver = fx.NewState("Receiver")
                .Shift(init, 1, 1);
            init.TransitionsTo(sender).When(fx.Av3().ItIsLocal());
            init.TransitionsTo(receiver).When(fx.Av3().ItIsRemote());
            
            // Fix the sender being stuck because drivers might not run on the first frame, causing focus to be zero
            sender.TransitionsTo(init).When(focus.IsEqualTo(0));

            // Respect VRC's "Don't dead end" rule
            sender.TransitionsTo(init).When(fx.Av3().ItIsRemote());
            receiver.TransitionsTo(init).When(fx.Av3().ItIsLocal());

            var numberOfBitsRequiredToEncodeAddress = packettization.NumberOfBitsRequiredToEncodeAddress();
            var packets = packettization.packets;
            for (var index = 0; index < packets.Length; index++)
            {
                var packet = packets[index];
                
                // We reserve packet number 0 as a signal for receivers that the avatar wearer is not initialized. This prevents the animator from being driven with incorrect values.
                var packetNumber = index + 1;

                var packetSender = fx.NewSubStateMachine($"Send Packet {packetNumber}")
                    .Shift(sender, 1, packetNumber);
                var packetReceiver = fx.NewSubStateMachine($"Receive Packet {packetNumber}")
                    .Shift(receiver, -1, packetNumber);

                var sendValues = packetSender.NewState("Send Values");
                var receiveValues = packetReceiver.NewState("Receive Values");

                // We don't want to create the original animator params if they already exist, as the animator may be making use of implicit casts.
                var silently = aac.NoAnimator();
                
                foreach (var packetParameter in packet.parameters)
                {
                    // BUT, we still need to create it if the animator doesn't have it (i.e. because it's a Gesture param or something)
                    if (!packetParameter.isDeclaredInAnimator)
                    {
                        switch (packetParameter.expressionParameterDeclaredType)
                        {
                            case VRCExpressionParameters.ValueType.Int:
                                fx.IntParameter(packetParameter.name);
                                break;
                            case VRCExpressionParameters.ValueType.Float:
                                fx.FloatParameter(packetParameter.name);
                                break;
                            case VRCExpressionParameters.ValueType.Bool:
                                fx.BoolParameter(packetParameter.name);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    
                        // FIXME: We need to know the parameter type!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    }
                }

                sendValues.DrivingLocally();
                receiveValues.DrivingLocally();

                // Sender focuses on next
                sendValues
                    .Drives(focus, index == packets.Length - 1 ? 1 : packetNumber + 1);

                // Sender focuses on current (for informative purposes only, this isn't used to drive internal state)
                receiveValues
                    .Drives(focus, packetNumber);

                // Set address
                for (var i = 0; i < numberOfBitsRequiredToEncodeAddress; i++)
                {
                    sendValues.Drives(fx.BoolParameter(MultiplexerAddressForBit(i)), ExtractBitFromPacketNumber(packetNumber, i));
                }

                // Send and receive value
                foreach (var packetParameter in packet.parameters)
                {
                    var magicallyTypedParam = silently.IntParameter(packetParameter.name);
                    switch (packetParameter.animatorDeclaredType)
                    {
                        case AnimatorControllerParameterType.Float:
                            sendValues.DrivingRemaps(magicallyTypedParam, -1, 1, fx.IntParameter(MultiplexerValue0()), 0, 255);
                            receiveValues.DrivingRemaps(fx.IntParameter(MultiplexerValue0()), 0, 255, magicallyTypedParam, -1, 1);
                            break;
                        case AnimatorControllerParameterType.Int:
                            sendValues.DrivingCopies(magicallyTypedParam, fx.IntParameter(MultiplexerValue0()));
                            receiveValues.DrivingCopies(fx.IntParameter(MultiplexerValue0()), magicallyTypedParam);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            // TODO: Bool packing
                            sendValues.DrivingRemaps(magicallyTypedParam, 0, 1, fx.IntParameter(MultiplexerValue0()), 0, 1);
                            receiveValues.DrivingRemaps(fx.IntParameter(MultiplexerValue0()), 0, 1, magicallyTypedParam, 0, 1);
                            break;
                        case AnimatorControllerParameterType.Trigger:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                sender.TransitionsTo(packetSender)
                    .AfterAnimationFinishes()
                    .When(fx.IntParameter(MultiplexerFocus()).IsEqualTo(packetNumber));

                receiver.TransitionsTo(packetReceiver)
                    .When(TheAddressIs(fx, packetNumber, numberOfBitsRequiredToEncodeAddress));

                sendValues.Exits().Automatically();
                receiveValues.Exits().Automatically();

                packetSender.TransitionsTo(sender);
                packetReceiver.TransitionsTo(receiver);
            }
        }

        private Action<AacFlTransitionContinuationWithoutOr> TheAddressIs(AacFlLayer fx, int packetNumber, int numberOfBitsRequiredToEncodeAddress)
        {
            return continuation =>
            {
                for (var i = 0; i < numberOfBitsRequiredToEncodeAddress; i++)
                {
                    continuation
                        .And(fx.BoolParameter(MultiplexerAddressForBit(i)).IsEqualTo(ExtractBitFromPacketNumber(packetNumber, i)));
                }
            };
        }

        private static bool ExtractBitFromPacketNumber(int packetNumber, int i)
        {
            return ((packetNumber >> i) & 1) > 0;
        }

        private string MultiplexerAddressForBit(int i)
        {
            return $"Multiplexer/Address/b{i}";
        }

        private static string MultiplexerValue0()
        {
            return "Multiplexer/Value";
        }

        private static string MultiplexerFocus()
        {
            return "Multiplexer/Focus";
        }
    }

    internal struct Packettization
    {
        public PacketUnit[] packets;

        public int NumberOfBitsRequiredToEncodeAddress()
        {
            // We do +1 because address number 0 is reserved.
            // - If we have 2 packets, we need 2 bits to encode the binary values 00 (Reserved), 01 (Packet 1), 10 (Packet 2).
            // - That makes Log2(3), which is 1.584...
            return Mathf.CeilToInt(Mathf.Log(packets.Length + 1, 2));
        }
    }

    internal struct PacketUnit
    {
        public ParamUnit[] parameters;
    }

    internal struct ParamUnit
    {
        public string name;
        public VRCExpressionParameters.ValueType expressionParameterDeclaredType;
        public bool isDeclaredInAnimator;
        public AnimatorControllerParameterType animatorDeclaredType;
    }
}