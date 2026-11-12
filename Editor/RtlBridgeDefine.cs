using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;

namespace RTLLocalizationStudio.Editor
{
    /// <summary>Enables the optional RTLTMPro bridge (RTL_LOCALIZATION_RTLTMPRO) only when an RTLTMPro assembly exists.</summary>
    [InitializeOnLoad]
    internal static class RtlBridgeDefine
    {
        private const string Define = "RTL_LOCALIZATION_RTLTMPRO";

        static RtlBridgeDefine() => EditorApplication.delayCall += Sync;

        private static void Sync()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool available = CompilationPipeline.GetAssemblies().Any(a => a.name == "RTLTMPro");
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
            bool has = defines.Contains(Define);
            if (available == has) return;
            PlayerSettings.SetScriptingDefineSymbols(target,
                available ? defines.Append(Define).ToArray() : defines.Where(d => d != Define).ToArray());
        }
    }
}
