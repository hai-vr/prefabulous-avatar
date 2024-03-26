using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
using AnimatorAsCode.V1.VRC;
using nadena.dev.ndmf;
using Prefabulous.Hai.Runtime;
using Prefabulous.VRC.Editor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using static Prefabulous.VRC.Editor.PrefabulousHaiFaceTrackingExtensionsPlugin;

[assembly: ExportsPlugin(typeof(PrefabulousHaiToeTrackingInternalPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousHaiToeTrackingInternalPlugin : PrefabulousAsCodePlugin<PrefabulousHaiToeTrackingInternal>
    {
        protected override PrefabulousAsCodePluginOutput Execute()
        {
            var bodyObj = context.AvatarRootTransform.Find("Body");
            if (bodyObj == null) return PrefabulousAsCodePluginOutput.Regular();
            
            var bodyMesh = bodyObj.GetComponent<SkinnedMeshRenderer>();
            if (bodyMesh == null) return PrefabulousAsCodePluginOutput.Regular();

            var fxi = CreateFXInverse();

            var holder = new GameObject
            {
                name = "HaiTT",
                transform =
                {
                    parent = my.transform
                }
            };

            var maAc = MaAc.Create(holder);
            maAc.NewMergeAnimator(fxi, VRCAvatarDescriptor.AnimLayerType.FX);

            return PrefabulousAsCodePluginOutput.Regular();
        }

        public enum TTActuatorFeature
        {
            Placeholder
        }

        private AacFlController CreateFXInverse()
        {
            var ctrl = aac.NewAnimatorController();
            var fx = ctrl.NewLayer();

            var copy = ctrl.NewLayer("CopyBigToeIsOver2ndToe");
            var it = copy.NewState("Copy");
            it.TransitionsTo(it).Automatically();
            it.DrivingCopies(fx.BoolParameter("right_bigtoe_is_over_secondtoe"), fx.FloatParameter("BigToeIsOver2ndToe"));

            var interpolationAmount = fx.FloatParameter("TT_Amount");
            fx.OverrideValue(interpolationAmount, 0.8f);


            var mega = MegaTree.NewMegaTree(aac, fx, fx.FloatParameter("HaiXT/One"));
            var featurette = Featurette<MegaTree>.NewFeaturette(mega);
            var actuatorFeats = Feats<TTActuatorFeature>.NewFeats(new HashSet<TTActuatorFeature>(new []{ TTActuatorFeature.Placeholder }));
            var tree = featurette
                .With(actuatorFeats.Has(TTActuatorFeature.Placeholder), mt => mt
                    .Interpolator(fx.FloatParameter("FirstDown"), fx.FloatParameter("right_bigtoe_curl"), interpolationAmount, MegaTree.InterpolatorRange.Analog)
                    .Interpolator(fx.FloatParameter("SecondDown"), fx.FloatParameter("right_secondtoe_curl"), interpolationAmount, MegaTree.InterpolatorRange.Analog)
                    .Interpolator(fx.FloatParameter("Splay"), fx.FloatParameter("right_fifthtoe_splay"), interpolationAmount, MegaTree.InterpolatorRange.Analog)
                )
                .Return.Tree;

            fx.NewState("Direct").WithWriteDefaultsSetTo(true).WithAnimation(tree);

            return ctrl;
        }
    }
}
