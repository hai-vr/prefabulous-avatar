using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
using AnimatorAsCode.V1.NDMFProcessor;
using nadena.dev.ndmf;
using Prefabulous.Hai.Runtime;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(PrefabulousHaiAccurateEyeTrackingPlugin))]
namespace Prefabulous.Hai.Runtime
{
    public class PrefabulousHaiAccurateEyeTrackingPlugin : AacPlugin<PrefabulousHaiAccurateEyeTracking>
    {
        protected override AacPluginOutput Execute()
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
                
            return AacPluginOutput.Regular();
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