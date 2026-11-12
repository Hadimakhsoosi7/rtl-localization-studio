# Changelog

## [1.0.1] - 2026-11-12
- `.unitypackage` release for drag-and-drop installs (imports into `Assets/RTLLocalizationStudio`).
- Offers to install Unity Localization automatically when it is missing (for `.unitypackage` installs).
- Assemblies compile only when Unity Localization is present, so a missing dependency no longer causes compile errors.
- "Open README" now works for git, tarball and `Assets` installs.

## [1.0.0] - 2026-11-12
- First standalone release.
- RTLTMPro bridge moved into the package (`RTL/`); enabled automatically via the `RTL_LOCALIZATION_RTLTMPRO` define when an `RTLTMPro` assembly exists.
- Studio folder pickers default to `Assets` instead of project-specific folders.
