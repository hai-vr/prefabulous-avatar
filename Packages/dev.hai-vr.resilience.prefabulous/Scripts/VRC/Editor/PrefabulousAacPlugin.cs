﻿using AnimatorAsCode.V1;
using JetBrains.Annotations;
using nadena.dev.ndmf;
using Prefabulous.Universal.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace Prefabulous.VRC.Editor
{
    // Do not attach the RunsOnPlatforms attribute here
    public class PrefabulousAsCodePlugin<T> : Plugin<PrefabulousAsCodePlugin<T>> where T : MonoBehaviour
    {
        // Can be changed if necessary
        [PublicAPI] protected virtual string SystemName(T script, BuildContext ctx) => typeof(T).Name;
        [PublicAPI] protected virtual Transform AnimatorRoot(T script, BuildContext ctx) => ctx.AvatarRootTransform;
        [PublicAPI] protected virtual Transform DefaultValueRoot(T script, BuildContext ctx) => ctx.AvatarRootTransform;
        [PublicAPI] protected virtual bool UseWriteDefaults(T script, BuildContext ctx) => false;

        // This state is short-lived, it's really just sugar
        [PublicAPI] protected AacFlBase aac { get; private set; }
        [PublicAPI] protected T my { get; private set; }
        [PublicAPI] protected BuildContext context { get; private set; }

        public override string QualifiedName => $"dev.hai-vr.prefabulous.vrc.ascode::{GetType().FullName}";
        public override string DisplayName => $"{typeof(T).Name}";

        protected virtual PrefabulousAsCodePluginOutput Execute()
        {
            return PrefabulousAsCodePluginOutput.Regular();
        }

        protected override void Configure()
        {
            if (GetType() == typeof(PrefabulousAsCodePlugin<>)) return;

            InPhase(BuildPhase.Generating)
                .Run($"Run PrefabulousAsCode for {GetType().Name}", ctx =>
                {
                    Debug.Log($"(self-log) Running aac plugin ({GetType().FullName}");

                    var scripts = ctx.AvatarRootObject.GetComponentsInChildren(typeof(T), true);
                    foreach (var currentScript in scripts)
                    {
                        var script = (T)currentScript;
                        aac = AacV1.Create(new AacConfiguration
                        {
                            SystemName = SystemName(script, ctx),
                            AnimatorRoot = AnimatorRoot(script, ctx),
                            DefaultValueRoot = DefaultValueRoot(script, ctx),
                            AssetKey = GUID.Generate().ToString(),
                            AssetContainer = ctx.AssetContainer,
                            ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
#if PREFABULOUS_NDMF_SUPPORTS_ASSETSAVER
                            AssetContainerProvider = new PrefabulousAsCodeContainerProvider(ctx),
#endif
                            DefaultsProvider = new AacDefaultsProvider(UseWriteDefaults(script, ctx))
                        });
                        my = script;
                        context = ctx;

                        Execute();
                    }

                    PrefabulousUtil.DestroyAllAfterBake<T>(ctx);

                    // Get rid of the short-lived sugar fields
                    aac = null;
                    my = null;
                    context = null;
                });
        }
    }

    public struct PrefabulousAsCodePluginOutput
    {
        public static PrefabulousAsCodePluginOutput Regular()
        {
            return new PrefabulousAsCodePluginOutput();
        }
    }
    
#if PREFABULOUS_NDMF_SUPPORTS_ASSETSAVER
    internal class PrefabulousAsCodeContainerProvider : IAacAssetContainerProvider
    {
        private readonly BuildContext _ctx;
        public PrefabulousAsCodeContainerProvider(BuildContext ctx) => _ctx = ctx;
        public void SaveAsPersistenceRequired(Object objectToAdd) => _ctx.AssetSaver.SaveAsset(objectToAdd);
        public void SaveAsRegular(Object objectToAdd) { } // Let NDMF crawl our assets when it finishes
        public void ClearPreviousAssets() { } // ClearPreviousAssets is never used in non-destructive contexts
    }
#endif
}