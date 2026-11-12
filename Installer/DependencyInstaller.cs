using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace RTLLocalizationStudio.Installer
{
    /// <summary>
    /// Installs Unity Localization when the toolkit was imported from a .unitypackage.
    /// UPM installs resolve this dependency on their own, so this stays idle there.
    /// Has no references so it compiles even before Unity Localization exists.
    /// </summary>
    [InitializeOnLoad]
    internal static class DependencyInstaller
    {
        private const string Package = "com.unity.localization";
        private const string SkipKey = "RTLLocalizationStudio.SkipDependencyInstall";
        private static AddRequest request;

        static DependencyInstaller() => EditorApplication.delayCall += Check;

        private static void Check()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization") != null) return;
            if (SessionState.GetBool(SkipKey, false)) return;
            if (!EditorUtility.DisplayDialog("RTL Localization Studio",
                    "RTL Localization Studio needs the Unity Localization package (" + Package + "). Install it now?",
                    "Install", "Later"))
            {
                SessionState.SetBool(SkipKey, true);
                return;
            }
            request = Client.Add(Package);
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!request.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (request.Status == StatusCode.Success) Debug.Log("RTL Localization Studio: installed " + request.Result.packageId);
            else Debug.LogError("RTL Localization Studio: could not install " + Package + ": " + request.Error?.message);
        }
    }
}
