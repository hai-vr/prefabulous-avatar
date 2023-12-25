using System;
using System.Collections.Generic;
using Prefabulous.Hai.Runtime;
using Resilience.Tooklit.Editor;

namespace Prefabulous.VRC.Editor
{
    public class PrefabulousLocalization : ResilienceLocalization
    {
        public static readonly PrefabulousLocalization Localization = new PrefabulousLocalization();

        public override string LocalePrefix() => "sampletemplate.";
        public override string InnerLocalePrefsKey() => "SampleTemplate.Locale";
        public override string Main() => "Packages/dev.hai-vr.resilience.sampletemplate/ResilienceSDK/SampleTemplate/Scripts/Editor/EditorUI/Locale";
        public override string Alternate() => "Assets/ResilienceSDK/SampleTemplate/Locale";
        public override string IntrospectionAutoFindTypeWithPrefix() => "SampleTemplate";

        public override void Introspect(HashSet<Type> visited)
        {
            IntrospectFields(typeof(PrefabulousHaiRecalculateNormals), visited);
            IntrospectFields(typeof(PrefabulousHaiGenerateBlendshapesFTE), visited);
            IntrospectInvokeAllPhrases(typeof(PrefabulousLocalizationPhrase));
        }
    }
}