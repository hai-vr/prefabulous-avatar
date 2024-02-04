﻿using System;
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

[assembly: ExportsPlugin(typeof(PrefabulousHaiMultiplexerPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousHaiMultiplexerPlugin : Plugin<PrefabulousHaiMultiplexerPlugin>
    {
        // VRC's update delay is supposedly 0.1 of a second (10 updates per second).
        // However, if the delay is set to exactly 0.1, there is a risk that the parameter value serialization will be performed in such a way that one of the packets will be skipped.
        // I'd rather have a duplicate than a skip, so I'll increase the delay here. 
        private const float DefaultUpdateDelaySeconds = 0.12f;
        
        private const string SendValues = "Send Values";
        private const string ReceiveValues = "Receive Values";

        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing)
                .Run("Expression Multiplexer", context =>
                {
                    var prefabulousComps = context.AvatarRootTransform.GetComponentsInChildren<PrefabulousHaiMultiplexer>(true);
                    if (prefabulousComps.Length == 0) return;

                    var forceStrategy = prefabulousComps.Any(multiplexer => multiplexer.useEvenWhenUnderLimit);

                    var expressionParameters = Object.Instantiate(context.AvatarDescriptor.expressionParameters);
                    context.AvatarDescriptor.expressionParameters = expressionParameters;
                    
                    var originalCost = expressionParameters.CalcTotalCost();
                    if (originalCost < MaxParamCost())
                    {
                        var msg = $"Total parameter cost ({originalCost}) is lower than maximum ({MaxParamCost()}.";
                        if (forceStrategy)
                        {
                            Debug.Log($"(Multiplexer) {msg}. No optimization is needed, however, at least one component has useStrategyEvenWhenUnderLimit, so we will process it regardless.");
                        }
                        else
                        {
                            Debug.Log($"(Multiplexer) {msg}. No optimization needed, skipping.");
                            return; // Don't process any further
                        }
                    }
                    else
                    {
                        Debug.Log($"(Multiplexer) The current total cost is {originalCost}.");
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
                    
                    Debug.Log($"(Multiplexer) We have a total of {lowUpdateRateParams.Count} LowUpdateRate parameters to send.");
                    Debug.Log($"(Multiplexer) The expression parameter currently has {expressionParameters.parameters.Length} parameters.");

                    var potentialCost = expressionParameters.CalcTotalCost();
                    var savingsWithoutAccountingForMultiplexer = originalCost - potentialCost;
                    Debug.Log($"(Multiplexer) Without accounting for the multiplexer cost, we're going from {originalCost} bits down to {potentialCost} bits, giving us savings of {savingsWithoutAccountingForMultiplexer} bits.");
                    
                    var leeway = MaxParamCost() - potentialCost;
                    Debug.Log($"(Multiplexer) The maximum is {MaxParamCost()} bits, so {MaxParamCost()} - {potentialCost} = {leeway} bits of leeway to work with.");

                    // TODO: Turn these low update rate params into packets
                    var fxAnimator = (AnimatorController)context.AvatarDescriptor.baseAnimationLayers
                        .First(layer => layer.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;

                    var packettization = TryPackettize(lowUpdateRateParams, expressionParameters.parameters, fxAnimator.parameters, leeway);

                    var newParams = expressionParameters.parameters.ToList();
                    newParams.Add(new VRCExpressionParameters.Parameter
                    {
                        name = MultiplexerValue(0),
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
                    
                    // Optional: Progress bar
                    if (newParams.All(parameter => parameter.name != MultiplexerProgress()))
                    {
                        newParams.Add(new VRCExpressionParameters.Parameter
                        {
                            name = MultiplexerProgress(),
                            networkSynced = false,
                            valueType = VRCExpressionParameters.ValueType.Float,
                            saved = false,
                            defaultValue = 0f
                        });
                    }
                    
                    expressionParameters.parameters = newParams.ToArray();
                    
                    // TODO: Handle having more than 8 bits of bandwidth for the value part
                    // TODO: Handle having less than 8 bits of bandwidth for the value part
                    // TODO: Handle having a non-multiple of 8 bits of bandwidth for the value part
                    CreateFxLayer(context, packettization, fxAnimator);
                });
        }

        private Packettization TryPackettize(
            Dictionary<string, PrefabulousHaiMultiplexerParameter> lowUpdateRateParams,
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

            var floatAndIntParams = paramUnits.Where(unit => unit.expressionParameterDeclaredType != VRCExpressionParameters.ValueType.Bool).ToList();
            var boolParams = new Queue<ParamUnit>(paramUnits.Where(unit => unit.expressionParameterDeclaredType == VRCExpressionParameters.ValueType.Bool).ToArray());
            
            var packets = new List<PacketUnit>();
            foreach (var param in floatAndIntParams)
            {
                packets.Add(new PacketUnit
                {
                    parameters = new[] { param },
                    kind = PacketKind.Single
                });
            }
            
            while (boolParams.Count > 0)
            {
                var parameters = new List<ParamUnit>();
                // We don't have LINQ Chunk?
                while (parameters.Count < 8 && boolParams.Count > 0)
                {
                    parameters.Add(boolParams.Dequeue());
                }
                packets.Add(new PacketUnit
                {
                    parameters = parameters.ToArray(),
                    kind = PacketKind.Bools
                });
            }

            // FIXME: Naive packettization
            // var packetUnits = paramUnits
            //     .Select(paramUnit => new PacketUnit
            //     {
            //         parameters = new[] { paramUnit }
            //     })
            //     .ToArray();

            return new Packettization
            {
                // packets = packetUnits
                packets = packets.ToArray()
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
                SystemName = "Multiplexer",
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
            var progressBar = fx.FloatParameter(MultiplexerProgress());
            
            var init = fx.NewState("Init")
                .Drives(focus, 1);

            var tempoAnimation = aac.DummyClipLasting(DefaultUpdateDelaySeconds, AacFlUnit.Seconds);

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
                    }
                }

                var progressBarAmount = (index + 1f) / packets.Length;
                var sendFocusNext = index == packets.Length - 1 ? 1 : packetNumber + 1;

                void SendStateConfFn(AacFlState sendValues, int packetNumber, float progressBarAmount, int sendFocusNext)
                {
                    sendValues.Drives(focus, sendFocusNext); // Sender focuses on next
                    sendValues.Drives(progressBar, progressBarAmount);
                    sendValues.DrivingLocally(); // Only the sender should drive locally. This is technically not necessary
                    for (var i = 0; i < numberOfBitsRequiredToEncodeAddress; i++)
                    {
                        sendValues.Drives(fx.BoolParameter(MultiplexerAddressForBit(i)), ExtractBitFromPacketNumber(packetNumber, i));
                    }
                }

                void ReceiveStateConfFn(AacFlState receiveValues, int packetNumber, float progressBarAmount)
                {
                    receiveValues.Drives(focus, packetNumber); // Receiver focuses on current (for informative purposes only, this isn't used to drive internal state)
                    receiveValues.Drives(progressBar, progressBarAmount);
                }
                
                // Send and receive value
                if (packet.kind == PacketKind.Single)
                {
                    var sendValues = packetSender.NewState(SendValues);
                    SendStateConfFn(sendValues, packetNumber, progressBarAmount, sendFocusNext);

                    var receiveValues = packetReceiver.NewState(ReceiveValues);
                    ReceiveStateConfFn(receiveValues, packetNumber, progressBarAmount);

                    var packetParameter = packet.parameters[0];
                    {
                        var magicallyTypedParam = silently.IntParameter(packetParameter.name);
                        var value = fx.IntParameter(MultiplexerValue(0));
                        switch (packetParameter.expressionParameterDeclaredType)
                        {
                            case VRCExpressionParameters.ValueType.Float:
                                // Float has 1 less possible value than Int to account for the representability of the value 0,
                                // so 254 is the max bound, not 255.
                                sendValues.DrivingRemaps(magicallyTypedParam, -1, 1, value, 0, 254);
                                receiveValues.DrivingRemaps(value, 0, 254, magicallyTypedParam, -1, 1);
                                break;
                            case VRCExpressionParameters.ValueType.Int:
                                sendValues.DrivingCopies(magicallyTypedParam, value);
                                receiveValues.DrivingCopies(value, magicallyTypedParam);
                                // sendValues.DrivingRemaps(magicallyTypedParam, 0, 255, value, -1, 1);
                                // receiveValues.DrivingRemaps(value, -1, 1, magicallyTypedParam, 0, 255);
                                break;
                            case VRCExpressionParameters.ValueType.Bool:
                                // NOTE: This branch should generally not be executed, as the PacketKind for bools is meant to be PacketKind.Bools
                                sendValues.DrivingCopies(magicallyTypedParam, value);
                                receiveValues.DrivingCopies(value, magicallyTypedParam);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    sendValues.Exits().Automatically();
                    receiveValues.Exits().Automatically();
                }
                else
                {
                    CreationMethod method = CreationMethod.OneSSMWith256States;
                    switch (method)
                    {
                        case CreationMethod.SingleDepthRecursiveSSM:
                            // FIXME: This is TOO SLOW to generate non-destructively
                            RecurseParameterSender(fx, packetSender, packet, 0, 0x0, sv => SendStateConfFn(sv, packetNumber, progressBarAmount, sendFocusNext));
                            RecurseParameterReceiver(fx, packetReceiver, packet, 0, 0x0, sv => ReceiveStateConfFn(sv, packetNumber, progressBarAmount), 0);
                            break;
                        case CreationMethod.OneSSMWith256States:
                            SingleSSMParameterSender(fx, packetSender, packet, sv => SendStateConfFn(sv, packetNumber, progressBarAmount, sendFocusNext));
                            SingleSSMParameterReceiver(fx, packetReceiver, packet, sv => ReceiveStateConfFn(sv, packetNumber, progressBarAmount));
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

                packetSender.TransitionsTo(sender);
                packetReceiver.TransitionsTo(receiver);
            }
        }

        internal enum CreationMethod
        {
            SingleDepthRecursiveSSM,
            OneSSMWith256States
        }

        private static void SingleSSMParameterSender(AacFlLayer fx, AacFlStateMachine ssm, PacketUnit packet, Action<AacFlState> sendStateConfFn)
        {
            var value = fx.IntParameter(MultiplexerValue(0));
            
            var totalNumberOfStates = (int)Math.Pow(2, packet.parameters.Length);
            for (var encoded = 0; encoded < totalNumberOfStates; encoded++)
            {
                var apply = ssm.NewState($"Apply{encoded}");
                apply.Drives(value, encoded);
                
                sendStateConfFn.Invoke(apply);
                
                apply.Exits().Automatically();

                var conditions = ssm.EntryTransitionsTo(apply).WhenConditions();
                for (var index = 0; index < packet.parameters.Length; index++)
                {
                    var param = packet.parameters[index];
                    conditions.And(fx.BoolParameter(param.name).IsEqualTo(((encoded >> index) & 1) > 0));
                }
            }
        }

        private static void SingleSSMParameterReceiver(AacFlLayer fx, AacFlStateMachine ssm, PacketUnit packet, Action<AacFlState> sendStateConfFn)
        {
            var value = fx.IntParameter(MultiplexerValue(0));
            
            var totalNumberOfStates = (int)Math.Pow(2, packet.parameters.Length);
            for (var encoded = 0; encoded < totalNumberOfStates; encoded++)
            {
                var apply = ssm.NewState($"Apply{encoded}");
                for (var index = 0; index < packet.parameters.Length; index++)
                {
                    var param = packet.parameters[index];
                    apply.Drives(fx.BoolParameter(param.name), ((encoded >> index) & 1) > 0);
                }
                
                sendStateConfFn.Invoke(apply);
                
                apply.Exits().Automatically();
                
                ssm.EntryTransitionsTo(apply).When(value.IsEqualTo(encoded));
            }
        }

        private static void RecurseParameterSender(AacFlLayer fx, AacFlStateMachine ssm, PacketUnit packet, int i, int bools, Action<AacFlState> sendStateConfFn)
        {
            if (i < packet.parameters.Length)
            {
                var param = packet.parameters[i];
                var fxParam = fx.BoolParameter(param.name);
                var whenFalse = ssm.NewSubStateMachine($"Index{i} False");
                var whenTrue = ssm.NewSubStateMachine($"Index{i} True");
                ssm.EntryTransitionsTo(whenFalse).When(fxParam.IsFalse());
                ssm.EntryTransitionsTo(whenTrue).When(fxParam.IsTrue());
                RecurseParameterSender(fx, whenFalse, packet, i + 1, bools, sendStateConfFn);
                RecurseParameterSender(fx, whenTrue, packet, i + 1, bools | (1 << i), sendStateConfFn);
                whenFalse.Exits();
                whenTrue.Exits();
            }
            else
            {
                var apply = ssm.NewState(SendValues);
                var encoded = (int)bools;
                apply.Drives(fx.IntParameter(MultiplexerValue(0)), encoded);

                sendStateConfFn.Invoke(apply);

                apply.Exits().Automatically();
            }
        }

        private static void RecurseParameterReceiver(AacFlLayer fx, AacFlStateMachine ssm, PacketUnit packet, int i, int bools, Action<AacFlState> receiveStateConfFn, int previousK)
        {
            var fxParam = fx.IntParameter(MultiplexerValue(0));
            if (i < packet.parameters.Length)
            {
                var k = previousK + (int)Math.Pow(2, packet.parameters.Length - 1 - i);
                
                var whenFalse = ssm.NewSubStateMachine($"Index{i} False");
                var whenTrue = ssm.NewSubStateMachine($"Index{i} True");
                ssm.EntryTransitionsTo(whenFalse).When(fxParam.IsLessThan(k));
                ssm.EntryTransitionsTo(whenTrue).When(fxParam.IsGreaterThan(k - 1));
                RecurseParameterReceiver(fx, whenFalse, packet, i + 1, bools << 1, receiveStateConfFn, previousK);
                RecurseParameterReceiver(fx, whenTrue, packet, i + 1, (bools << 1) | 1, receiveStateConfFn, k);
                whenFalse.Exits();
                whenTrue.Exits();
            }
            else
            {
                var apply = ssm.NewState(ReceiveValues);
                for (var index = 0; index < packet.parameters.Length; index++)
                {
                    var packetParameter = packet.parameters[index];
                    var decodedValue = ((bools >> index) & 1) > 0;
                    apply.Drives(fx.BoolParameter(packetParameter.name), decodedValue);
                }

                receiveStateConfFn.Invoke(apply);

                apply.Exits().Automatically();
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
            return $"Mux/Sync/Addr/b{i}";
        }

        private static string MultiplexerValue(int v)
        {
            return $"Mux/Sync/Value/{v}";
        }

        private static string MultiplexerFocus()
        {
            return "Mux/Local/Focus";
        }

        private static string MultiplexerProgress()
        {
            return "Mux/Local/Progress";
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
        public PacketKind kind;
        public ParamUnit[] parameters;
    }

    internal enum PacketKind
    {
        Single,
        Bools
    }

    internal struct ParamUnit
    {
        public string name;
        public VRCExpressionParameters.ValueType expressionParameterDeclaredType;
        public bool isDeclaredInAnimator;
        public AnimatorControllerParameterType animatorDeclaredType;
    }
}