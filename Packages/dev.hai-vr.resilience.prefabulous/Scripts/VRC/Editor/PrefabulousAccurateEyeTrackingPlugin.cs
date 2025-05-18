using System;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Prefabulous.VRC.Editor;
using Prefabulous.VRC.Runtime;

[assembly: ExportsPlugin(typeof(PrefabulousAccurateEyeTrackingPlugin))]
namespace Prefabulous.VRC.Editor
{
#if PREFABULOUS_NDMF_CROSSAPP_INTEGRATION_SUPPORTED
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
#endif
    public class PrefabulousAccurateEyeTrackingPlugin : PrefabulousAsCodePlugin<PrefabulousAccurateEyeTracking>
    {
        public override string QualifiedName => "dev.hai-vr.prefabulous.vrc.AccurateEyeTracking";
        public override string DisplayName => "Prefabulous for VRChat - Accurate Eye Tracking";
        
        protected override PrefabulousAsCodePluginOutput Execute()
        {
            var fx = aac.NewAnimatorController();
            var layer = fx.NewLayer();

            var one = layer.FloatParameter("AccurateEyes/One");
            layer.OverrideValue(one, 1);

            Params(out var lx, out var rx, out var ly, out var ry);
            
            var leftX = layer.FloatParameter(lx);
            var rightX = layer.FloatParameter(rx);
            var leftY = layer.FloatParameter(ly);
            var rightY = layer.FloatParameter(ry);

            var dbt = aac.NewBlendTree().Direct()
                .WithAnimation(TreeFor(leftX, my.leftEyeYaw, false), one)
                .WithAnimation(TreeFor(leftY, my.leftEyePitch, true), one)
                .WithAnimation(TreeFor(rightX, my.rightEyeYaw, false), one)
                .WithAnimation(TreeFor(rightY, my.rightEyePitch, true), one);

            layer.NewState("DBT").WithWriteDefaultsSetTo(true).WithAnimation(dbt);

            var holder = new GameObject
            {
                name = "AccurateEyeTracking",
                transform =
                {
                    parent = my.transform
                }
            };

            var ma = MaAc.Create(holder);
            ma.NewMergeAnimator(fx, VRCAvatarDescriptor.AnimLayerType.FX);
                
            return PrefabulousAsCodePluginOutput.Regular();
        }

        private void Params(out string lx, out string rx, out string ly, out string ry)
        {
            switch (my.vendor)
            {
                case PrefabulousAccurateEyeTrackingAnimatorVendor.Adjerry91:
                    lx = "OSCm/Proxy/FT/v2/EyeLeftX";
                    rx = "OSCm/Proxy/FT/v2/EyeRightX";
                    ly = "OSCm/Proxy/FT/v2/EyeY";
                    ry = "OSCm/Proxy/FT/v2/EyeY";
                    break;
                case PrefabulousAccurateEyeTrackingAnimatorVendor.Custom:
                    lx = my.leftXParam;
                    rx = my.rightXParam;
                    ly = my.leftYParam;
                    ry = my.rightYParam;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private AacFlBlendTree1D TreeFor(AacFlFloatParameter param, Transform control, bool isPitch)
        {
            var tree = aac.NewBlendTree().Simple1D(param);
            
            // Neither quaternion interpolation nor Euler interpolation appear to produce accurate results.
            // Instead, chop the normalized cartesian angle into equal parts, and map to an angle.
            for (var i = -90; i <= 90; i = i + 2)
            {
                var normalizedAngle = i / 90f;
                var arcsin = Mathf.Rad2Deg * Mathf.Asin(normalizedAngle);
                var vec = new Vector3();
                if (isPitch)
                {
                    vec.x = -arcsin;
                }
                else
                {
                    vec.y = arcsin;
                }
                tree.WithAnimation(aac.NewClip().RotatingUsingEulerInterpolation(new []{ control.gameObject }, vec), normalizedAngle);
            }

            return tree;
        }
    }
}
