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

                    expressionParameters.parameters = expressionParameters.parameters.ToList()
                        .Append(new VRCExpressionParameters.Parameter
                        {
                            name = MultiplexerValue0(),
                            networkSynced = true,
                            valueType = VRCExpressionParameters.ValueType.Int,
                            saved = false,
                            defaultValue = 0f
                        })
                        .Append(new VRCExpressionParameters.Parameter
                        {
                            name = MultiplexerAddressB0(),
                            networkSynced = true,
                            valueType = VRCExpressionParameters.ValueType.Bool,
                            saved = false,
                            defaultValue = 0f
                        })
                        .ToArray();

                    // TODO: Turn these low update rate params into packets
                    TryPackettize(lowUpdateRateParams, expressionParameters.parameters.Where(parameter => lowUpdateRateParams.ContainsKey(parameter.name)).ToArray(), leeway);
                    
                    // TODO: Group up lowUpdateRateParams by type to send in bulks
                    // TODO: Handle more than one address bit
                    // TODO: Handle having more than 8 bits of bandwidth for the value part
                    // TODO: Handle having less than 8 bits of bandwidth for the value part
                    // TODO: Handle having a non-multiple of 8 bits of bandwidth for the value part
                    CreateFxLayer(context, lowUpdateRateParams);
                });
        }

        private void TryPackettize(Dictionary<string, PrefabulousHaiExpressionMultiplexerParameter> lowUpdateRateParams, VRCExpressionParameters.Parameter[] filteredParameters, int leeway)
        {
        }

        private static int MaxParamCost()
        {
            return VRCExpressionParameters.MAX_PARAMETER_COST;
        }

        private void CreateFxLayer(BuildContext ctx,
            Dictionary<string, PrefabulousHaiExpressionMultiplexerParameter> lowUpdateRateParams)
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
            var runtimeAnimatorController = ctx.AvatarDescriptor.baseAnimationLayers
                .First(layer => layer.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
            
            var ctrl = (AnimatorController)runtimeAnimatorController;
            var fx = aac.CreateMainArbitraryControllerLayer(ctrl);

            var focus = fx.IntParameter(MultiplexerFocus());
            var init = fx.NewState("Init")
                .Drives(focus, 1);

            // FIXME: There is a risk of having a skip if we use a delay of exactly 0.1 sec, which is supposed to be VRC's update rate.
            var tempoAnimation = aac.DummyClipLasting(0.12f, AacFlUnit.Seconds);

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

            var lowUpdateRates = lowUpdateRateParams.Values.ToArray();
            for (var index = 0; index < lowUpdateRates.Length; index++)
            {
                var lowUpdateRate = lowUpdateRates[index];
                var packetNumber = index + 1;

                var packetSender = fx.NewSubStateMachine($"Send Packet {packetNumber}")
                    .Shift(sender, 1, packetNumber);
                var packetReceiver = fx.NewSubStateMachine($"Receive Packet {packetNumber}")
                    .Shift(receiver, -1, packetNumber);

                var sendValues = packetSender.NewState("Send Values");
                var receiveValues = packetReceiver.NewState("Receive Values");

                // We don't want to create the original animator params if they already exist, as the animator may be making use of implicit casts.
                var silently = aac.NoAnimator();
                var magicallyTypedParam = silently.IntParameter(lowUpdateRate.name);

                {
                    // BUT, we still need to create it if the animator doesn't have it (i.e. because it's a Gesture param or something)
                    var itDoesNotExist = ctrl.parameters.All(parameter => parameter.name != lowUpdateRate.name);
                    
                    // FIXME: We need to know the parameter type!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    fx.FloatParameter(lowUpdateRate.name);
                }

                // Sender focuses on next
                sendValues
                    .Drives(focus, index == lowUpdateRates.Length - 1 ? 1 : packetNumber + 1);

                // Sender focuses on current (for informative purposes only, this isn't used to drive internal state)
                receiveValues
                    .Drives(focus, packetNumber);

                // Set address
                sendValues
                    .Drives(fx.BoolParameter(MultiplexerAddressB0()), true);

                // Send and receive value
                sendValues
                    .DrivingRemaps(magicallyTypedParam, -1, 1, fx.IntParameter(MultiplexerValue0()), 0, 255)
                    .DrivingLocally();
                receiveValues
                    .DrivingRemaps(fx.IntParameter(MultiplexerValue0()), 0, 255, magicallyTypedParam, -1, 1)
                    .DrivingLocally();

                sender.TransitionsTo(packetSender)
                    .AfterAnimationFinishes()
                    .When(fx.IntParameter(MultiplexerFocus()).IsEqualTo(packetNumber));

                receiver.TransitionsTo(packetReceiver)
                    .When(TheAddressIs(fx, packetNumber));

                sendValues.Exits().Automatically();
                receiveValues.Exits().Automatically();

                packetSender.TransitionsTo(sender);
                packetReceiver.TransitionsTo(receiver);
            }
        }

        private Action<AacFlTransitionContinuationWithoutOr> TheAddressIs(AacFlLayer fx, int packetNumber)
        {
            return continuation => continuation
                .And(fx.BoolParameter(MultiplexerAddressB0()).IsEqualTo(packetNumber == 1));
        }

        private static string MultiplexerAddressB0()
        {
            return "Multiplexer/Address/b0";
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
}