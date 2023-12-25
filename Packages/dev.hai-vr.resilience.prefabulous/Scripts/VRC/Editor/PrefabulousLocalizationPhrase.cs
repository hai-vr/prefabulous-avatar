namespace Prefabulous.VRC.Editor
{
    public class PrefabulousLocalizationPhrase
    {
        public static string AddComponentLabel => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(AddComponentLabel), "Add \"PA-H HaiXT Face Tracking Extensions\" component");
        public static string FaceMeshLabel => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(FaceMeshLabel), "Face Mesh");
        public static string LeftEyeClosedSmilingLabel => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(LeftEyeClosedSmilingLabel), "Left Eye Closed (smiling)");
        public static string LeftLabel => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(LeftLabel), "Left");
        public static string MsgExplainHaiXT_EyeClosedInverse_Smile => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(MsgExplainHaiXT_EyeClosedInverse_Smile), "Non-standard shape for anime-like avatars: Closes the eyes with the eyelids going up, like the ^_^ smiley.");
        public static string MsgMissingBody => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(MsgMissingBody), "Your avatar does not appear to have a face mesh called \"Body\".");
        public static string MsgMissingComponent => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(MsgMissingComponent), "No \"PA-H HaiXT Face Tracking Extensions\" component was found on this avatar. Add one?");
        public static string MsgMissingPreconditionForHaiXT_EyeClosedInverse_Smile => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(MsgMissingPreconditionForHaiXT_EyeClosedInverse_Smile), "Your avatar does not appear to have the required EyeClosedLeft and EyeClosedRight face tracking blendshapes.");
        public static string MsgNoSuchBlendshapeName => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(MsgNoSuchBlendshapeName), "This blendshape does not exist.");
        public static string RightEyeClosedSmilingLabel => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(RightEyeClosedSmilingLabel), "Right Eye Closed (smiling)");
        public static string RightLabel => PrefabulousLocalization.Localization.LocalizeOrElse(nameof(RightLabel), "Right");
    }
}