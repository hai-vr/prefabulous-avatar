using AnimatorAsCode.V1.ModularAvatar;
using nadena.dev.ndmf;
using Prefabulous.Universal.Shared.Editor;
using Prefabulous.VRC.Editor;
using Prefabulous.VRC.Runtime;

[assembly: ExportsPlugin(typeof(PrefabulousImportExpressionParametersPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousImportExpressionParametersPlugin : PrefabulousAsCodePlugin<PrefabulousImportExpressionParameters>
    {
        public override string QualifiedName => "dev.hai-vr.prefabulous.vrc.ImportExpressionParameters";
        public override string DisplayName => "Prefabulous for VRChat - Import Expressions Parameters";
        
        protected override PrefabulousAsCodePluginOutput Execute()
        {
            if (my.parameters == null) return PrefabulousAsCodePluginOutput.Regular();
            
            var ma = MaAc.Create(my.gameObject);
            ma.ImportParameters(my.parameters);
            
            return PrefabulousAsCodePluginOutput.Regular();
        }
    }
}
