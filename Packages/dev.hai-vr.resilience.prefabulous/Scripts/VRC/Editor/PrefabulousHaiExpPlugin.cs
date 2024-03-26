using AnimatorAsCode.V1.ModularAvatar;
using nadena.dev.ndmf;
using Prefabulous.Hai.Runtime;
using Prefabulous.VRC.Editor;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(PrefabulousHaiExpPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousHaiExpPlugin : PrefabulousAsCodePlugin<PrefabulousHaiExp>
    {
        protected override PrefabulousAsCodePluginOutput Execute()
        {
            var ctrl = aac.NewAnimatorController();
            var fx = ctrl.NewLayer();
            var ssm = fx.NewSubStateMachine("SSM");
            var a = ssm.NewState("A");
            var b = ssm.NewState("B");
            fx.AnyTransitionsTo(b);

            var maAc = MaAc.Create(context.AvatarRootTransform.gameObject);
            maAc.NewMergeAnimator(ctrl, VRCAvatarDescriptor.AnimLayerType.FX);

            return PrefabulousAsCodePluginOutput.Regular();
        }
    }
}
