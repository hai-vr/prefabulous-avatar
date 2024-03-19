using AnimatorAsCode.V1;
using AnimatorAsCode.V1.NDMFProcessor;
using JetBrains.Annotations;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

namespace Prefabulous.VRC.Editor
{
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

        public override string QualifiedName => $"dev.hai-vr.ndmf-processor::{GetType().FullName}";
        public override string DisplayName => $"NdmfAsCode for {GetType().Name}";

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
                })
                .BeforePlugin((NdmfAacDBTPlugin)NdmfAacDBTPlugin.Instance);
        }
    }

    public struct PrefabulousAsCodePluginOutput
    {
        public static PrefabulousAsCodePluginOutput Regular()
        {
            return new PrefabulousAsCodePluginOutput();
        }
    }
}