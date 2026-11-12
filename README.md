# RTL Localization Studio

A Unity 6 editor + runtime toolkit built on top of [Unity Localization](https://docs.unity3d.com/Packages/com.unity.localization@latest), with first-class support for right-to-left languages (Persian, Arabic, Sorani Kurdish).

- Bulk-connect every text in your scenes and prefabs to translation keys in one pass
- CSV export/import with preview, validation and a translation review ledger
- Per-language font mapping, glyph coverage check and project-wide font scan
- Live language preview in Scene/Game view without entering Play Mode
- Automatic translation of text written from scripts via `.text` / `SetText`
- RTL shaping, Persian/Arabic digit conversion and selective layout mirroring
- Optional [RTLTMPro](https://github.com/pnarimani/RTLTMPro) bridge that turns itself on when RTLTMPro is present

Open **Tools > RTL Localization Studio > Open Studio**.

## Installation

**Git URL (recommended).** Add to the `dependencies` section of `Packages/manifest.json`:

```json
"com.hadimakhsoosi7.rtl-localization-studio": "https://github.com/hadimakhsoosi7/rtl-localization-studio.git#v1.0.0"
```

Or in Unity: **Window > Package Manager > + > Add package from git URL…** and paste the same URL. The `#v1.0.0` suffix pins the version; change the tag to upgrade.

**Local.** Copy the folder into your project's `Packages/` directory (embedded package), or reference it with `"com.hadimakhsoosi7.rtl-localization-studio": "file:D:/path/to/rtl-localization-studio"`.

On first compile the package sets itself up automatically (creates `Assets/Localization` and the Localization settings).

**RTLTMPro (optional).** If RTLTMPro is in the project, the `RTL/` bridge is enabled automatically through the `RTL_LOCALIZATION_RTLTMPRO` define. For full `SetText` support, apply `Documentation~/RTLTextMeshPro.patch` to that project's `RTLTextMeshPro.cs` (`git apply`).

Requirements: Unity 6 (6000.0+), `com.unity.localization` 1.5.13+, TextMeshPro (included in uGUI 2.0).

## Languages and fonts

29 languages are ready to use: Persian `fa`, English `en`, Arabic `ar`, Simplified Chinese `zh-Hans`, Korean `ko`, Japanese `ja`, French `fr`, German `de`, Spanish `es`, Italian `it`, Dutch `nl`, Norwegian Bokmål `nb`, Portuguese `pt`, Turkish `tr`, Finnish `fi`, Russian `ru`, Traditional Chinese `zh-Hant`, Malay `ms`, Indonesian `id`, Thai `th`, Vietnamese `vi`, Polish `pl`, Tagalog `tl`, Ukrainian `uk`, Hindi `hi`, Greek `el`, Kurmanji Kurdish (Latin script) `ku`, Sorani Kurdish (Arabic script, right-to-left) `ckb` and Bengali `bn`. Setup creates their tables and Locales and keeps existing settings. New languages fall back to the source language (Persian) until translations are imported. Newly exported CSVs contain every language; older three-language CSVs can still be imported and missing columns are left untouched.

In the **Fonts** tab:

1. Set the folder to `Assets` and press **Scan all texts & fonts**. Scenes, prefabs and inactive texts are included. Scene instances and prefab assets are counted separately.
2. Use **Show texts** to see the text, asset path and hierarchy path of every font usage. TMP font assets and TTF/OTF files can be listed too.
3. Pick a language and set a **replacement** next to each original font. For example, you can map a heading font and a body font to two different Japanese fonts.
4. Connected texts use this mapping. To switch fonts on everything, including empty texts and texts without a translation key, press **Enable font switching** for that group. Save open scenes. Saved assets are backed up to `Library/RTLLocalizationStudioBackups` before being modified.
5. Press **Save font rules** and check the result with **Preview language in open scenes** without entering Play Mode. **Restore preview** brings back the saved fonts and appearance. Removing a replacement falls back to the language's default font, or to the original font.

TMP font priority: mapping for that font → language default font in Overview → original font. Legacy UI Text and TextMesh use their own Font/TTF mapping. `LocalizedFont` and `LocalizedLegacyFont` do not translate text; they only switch fonts. Connected prefabs instantiated at runtime behave the same way. Objects built entirely from code need this component.

**Check glyph coverage** checks the characters of existing translations against the TMP font atlas and its fallbacks and lists missing code points. Tags and placeholders are skipped. Dynamic fonts may add glyphs at runtime; this check does not modify the atlas. Having the glyphs does not by itself guarantee correct rendering of complex scripts, so preview the result with the real font. Choose a suitable font for each language yourself; adding a language does not download fonts or translations.

The latest report is kept in `Library/RTLLocalizationStudio` and can be reopened with **Load last scan**. **Export usage CSV** writes the TMP report for review outside Unity; a separate legacy-font report is saved in the same folder.

## Quick start

1. On first compile, Persian, English and Arabic settings and a `Game` table are created. If setup did not finish, press **Set up localization** in Overview.
2. In **Bulk setup**, choose a scope. **Loaded scenes** or **Selected scene objects** is a good start.
3. Press **Scan texts**. Empty, numeric, input-field and ignored texts are not selected automatically.
4. Review keys and texts, then press **Connect selected**. Bulk connecting does not change how the text looks. Save the open scene yourself.
5. Enter translations in **Translations**, or export a CSV. Edit the UTF-8 file in Excel or Google Sheets and export it back to CSV.
6. **Preview CSV import** shows the changes before applying them. Use **Full review report** for the complete list.
7. Text direction only changes when **FollowLanguage** is selected. Reordering elements is handled separately by the `LocalizedLayout` component.

The **SCENE LANGUAGE** bar is available above every Studio tab. Click **Persian / FA**, **English / EN** or **Arabic / AR** to preview all connected texts in loaded scenes and Prefab Mode without Play. Inactive objects are included. Missing translations fall back to the Persian source. Click the same language again to refresh after editing translations. Scene and Game views redraw using the font, digit and alignment settings of each label.

**Restore** returns the exact authored text and styles. Closing Studio, saving assets/scenes, entering Play Mode or compiling scripts ends the preview automatically. Preview uses Unity's temporary Animation Mode snapshot and does not change the saved player language. Stop an Animation-window preview before starting a language preview. Dynamic Smart templates without their runtime arguments keep their placeholders visible. Scene preview affects localized text; sibling reordering from LocalizedLayout still runs in Play Mode.

The Studio UI uses English labels because Unity IMGUI does not reliably shape Persian text. The dedicated **Preview on selected text** window remains available for an isolated single-label preview. Its language buttons change rendering style; edit its text area to inspect another translation.

## Runtime

### Writing text with plain `.text` and `SetText`

Connected texts have **Auto translate written text** enabled by default. In Play Mode, writing source-language (Persian) text with the usual calls looks up its translation in the table and re-renders it when the language changes:

```csharp
title.text = playSource;              // a Persian source string registered in the table
score.SetText(scoreSource, 120);      // e.g. the Persian equivalent of "Score: {0}"
```

The source text must be registered in the table and have a translation. For the second example, register the Persian row for `Score: {0}` and the English row `Score: {0}`. Numbered numeric templates are supported; sentences built from player names or arbitrary text need an explicit template and binding. Unknown text, user input and ambiguous text without an explicit key are left unchanged. This is not online translation and it never guesses meaning.

For strings inside scripts, use **Code audit > Find Persian source phrases in code**, review the phrases and press **Add reviewed phrases to translation table**; then enter translations in Translations or via CSV. The scan finds constant strings, some simple conditionals and `SetText` calls; it does not extract every possible C# combination.

For UIs that are only filled from code, select the relevant texts (including empty ones) in **Bulk setup** after scanning and press **Attach auto translation for script-written text**. Binding happens in bulk, so there is no need to add the component to each text by hand. Save the modified scene or prefab. Objects created entirely at runtime need this binding in their prefab or at creation time.

Changes are detected in `LateUpdate`, so the getter can still return the source text right after writing. Each text is looked up only when its value changes. With many active texts, measure the per-frame cost in the Profiler. For a specific UI you can turn the automatic option off and use the key-based API below.

The RTL bridge comes with a compatibility change to `RTLTextMeshPro.cs` so that inherited `SetText` calls are preserved too. The change is in `Documentation~/RTLTextMeshPro.patch` and must be applied in every project that uses RTLTMPro.


```csharp
using RTLLocalizationStudio;

Localization.SetLanguage("en"); // fa / en / ar; persisted locally
label.SetKey("menu.play");
label.SetKey("game.score", new { score = 120 });
label.SetArguments(new { score = 150 });
```

Enable **Smart parameters** for parameterized entries. Supply complete sentences instead of concatenating localized fragments. Unity Smart Strings supports locale-aware plural forms. `LocalizedLabel` keeps the key and arguments and refreshes on locale changes, including labels enabled later. UI buttons can invoke `LanguageButton.SelectLanguage` through OnClick.

Tables are preloaded by Unity Localization/Addressables and shipped locally by default. No server, translator account or internet connection is needed by the player. Include the generated `Assets/AddressableAssetsData` and `Assets/Localization` in version control. Build Addressables content when making a player build according to the project's Addressables build settings.

## Fonts, RTL, digits and layout

Assign per-language fonts in Overview when needed; an unassigned font keeps the label's original font. Fonts must include the glyphs required by RTL shaping. Configure TMP fallback fonts on the font asset itself. Mixed-script strings, punctuation, multiline wrapping and Arabic diacritics need visual review using the actual game fonts.

`RTL/` (assembly `RTLLocalizationStudio.RTL`) is the optional bridge for an RTLTMPro installation. It compiles only when the `RTL_LOCALIZATION_RTLTMPRO` define is set; `RtlBridgeDefine` adds/removes it automatically for the active build target depending on whether an `RTLTMPro` assembly exists. Bulk setup adds the bridge when that assembly is available. It handles both ordinary TMP labels and existing RTLTextMeshPro components. English labels that fall back to Persian are still shaped. Projects without the bridge retain standard TMP rendering.

Digit conversion is explicitly enabled per label. IDs, phone numbers, URLs and user content should normally retain their digits. Rich-text tag attributes are not converted.

`LocalizedLayout.children` should contain siblings **in their original ascending hierarchy order**. `originalIsRightToLeft` describes the authored layout. Only those siblings and the optional directional icon change. Never point the icon field at a Canvas or a text hierarchy.

## Bulk migration and recovery

- Scan prefab folders before scene folders so instances inherit their bindings.
- Keys are based on the asset identity and hierarchy, and stored once in the component. Renaming/moving a connected object does not regenerate its key.
- Identical source strings share keys only when you explicitly choose **Share identical text keys**. Do not merge homonyms with different meanings.
- Existing labels and Ignore Localization markers are skipped. Add `LocalizationIgnore` with `includeChildren` to exclude a whole user-content subtree.
- Input fields are flagged for review. Localize their placeholders separately; do not bind their editable text.
- Script-generated text cannot be migrated safely through component attachment alone. Use Code audit to find `.text =` and `.SetText(...)`; adapt game-state messages to `SetKey`/`SetArguments`. The audit is a textual search, not complete C# data-flow analysis.
- Binding changes in loaded scenes support Unity Undo. Save those scenes yourself.
- Saved prefab and closed-scene changes are saved immediately. Before modification, exact originals are copied to `Library/RTLLocalizationStudioBackups/<timestamp>/Assets/...`. These disk writes are **not** fully undoable through Unity Undo. To restore, close the affected scene/Prefab Mode, copy the chosen original file back to its matching Assets path, and refresh Unity. New translation keys can remain unused after restoration; remove them intentionally if necessary.
- Bulk scans do not change assets. If a source changes after a scan, that row is skipped: rescan rather than applying stale content.
- Nested prefab/variant overrides are not automatically merged into source prefabs when their authored text differs. Review these separately to preserve intended overrides.

## CSV contract

Columns: `Key,Id,fa,en,ar,Smart,SourceHash`. Translator-editable columns are `en` and `ar` only. Keys, IDs, source fingerprints and Smart flags are checked before import. Persian changes happen in Studio or Unity String Tables, then a fresh CSV is exported. Empty cells preserve existing translations. Intentionally clear translations in Studio.

Quoted commas, multiline cells, quotes and UTF-8 BOM are supported. Unknown IDs, duplicate keys, malformed quoting, stale source text and differing Smart placeholder names block import. Import preview itself is read-only. Apply rechecks that translations and source text have not changed since preview. Keep spreadsheet ID columns as text; numeric rounding will be rejected as an ID mismatch.

The editor-only review ledger marks a translated entry as needing review if its Persian source changes. Imported translations are marked reviewed against their source fingerprint. Manually edited translations can be marked reviewed with the corresponding button. Unity String Tables remain the only runtime translation database.

## Reuse

See **Installation** above. Unity resolves Localization and UGUI dependencies. The RTLTMPro bridge ships inside the package and stays inactive in projects without RTLTMPro. Project-specific data lives in `Assets/Localization`; do not copy game data when starting a different project. First-run setup creates a fresh workspace.

## Verification

**Tools > RTL Localization Studio > Validate Localization** runs integration checks and writes `Logs/localization-validation.txt`. It uses temporary objects in a preview scene and removes its temporary table keys. This checks CSV round trips, malformed input, digit conversion, workspace/fallback/preload, idempotent binding and import behavior. It does not replace visual testing of all scenes or an Android player build.

Recommended acceptance pass: switch fa → en → ar → fa; open an inactive popup; verify saved language after restart; inspect mixed names/numbers, Arabic diacritics and missing-translation fallback; check selected layout elements and a real player build.
