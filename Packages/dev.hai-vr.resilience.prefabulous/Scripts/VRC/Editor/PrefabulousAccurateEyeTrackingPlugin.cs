﻿#if PREFABULOUS_INTERNAL
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

            var leftX = layer.FloatParameter("OSCm/Proxy/FT/v2/EyeLeftX");
            var rightX = layer.FloatParameter("OSCm/Proxy/FT/v2/EyeRightX");
            var anyY = layer.FloatParameter("OSCm/Proxy/FT/v2/EyeY");

            var dbt = aac.NewBlendTree().Direct()
                .WithAnimation(TreeFor(leftX, my.leftEyeYaw, false), one)
                .WithAnimation(TreeFor(anyY, my.leftEyePitch, true), one)
                .WithAnimation(TreeFor(rightX, my.rightEyeYaw, false), one)
                .WithAnimation(TreeFor(anyY, my.rightEyePitch, true), one);

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

        private AacFlBlendTree1D TreeFor(AacFlFloatParameter param, Transform control, bool isPitch)
        {
            var tree = aac.NewBlendTree().Simple1D(param);
            
            // Neither quaternion interpolation nor Euler interpolation produce accurate results.
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
#endif