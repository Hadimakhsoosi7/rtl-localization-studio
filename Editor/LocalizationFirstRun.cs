using UnityEditor;
using UnityEngine;
namespace RTLLocalizationStudio.Editor
{
    [InitializeOnLoad]
    internal static class LocalizationFirstRun
    {
        static LocalizationFirstRun() => EditorApplication.delayCall += Initialize;
        private static void Initialize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || (LocalizationWorkspace.Profile && LocalizationWorkspace.Profile.schemaVersion >= LanguageCatalog.SchemaVersion)) return;
            try { LocalizationWorkspace.Setup(); Debug.Log("RTL Localization Studio ready: Tools > RTL Localization Studio > Open Studio"); }
            catch (System.Exception ex) { Debug.LogWarning("Setup can be retried from Localization Studio: " + ex.Message); }
        }
    }
}
