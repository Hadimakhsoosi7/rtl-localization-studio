using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RTLLocalizationStudio.Editor
{
    public sealed class LocalizationStudio : EditorWindow
    {
        private enum Page { Dashboard, Connect, Translate, Scripts, Fonts, Settings, Help }
        private static readonly string[] PageNames = { "Dashboard", "Connect texts", "Translate", "Script texts", "Fonts", "Settings", "Help" };
        private static readonly string[] PageHints = { "Progress and next step", "Bind scene & prefab texts", "Edit, import, export", "Strings written from C#", "Fonts per language", "Workspace & styles", "Workflow & API" };
        private static readonly string[] PageDescriptions = {
            "Where your translation work stands and what to do next.",
            "Scan scenes and prefabs, then bind their TMP texts to translation keys.",
            "Edit translations per language, or round-trip them through a CSV file.",
            "Find Persian strings written from C# and register them for translation.",
            "Replace fonts per language so every script has the glyphs it needs.",
            "Workspace health, language styles and validation.",
            "The workflow and the runtime API at a glance."
        };
        private static readonly string[] TargetCodes = LocalizationWorkspace.Codes.Where(x => x != "fa").ToArray();
        private const float SidebarWidth = 210, Pad = 20;
        private const int PageSize = 30;

        [SerializeField] private Page page;
        private readonly LocalizationFontPanel fontPanel = new LocalizationFontPanel();
        private SearchField searchField;
        private Vector2 scroll, sidebarScroll;
        private int listPage;
        private string search = "";
        private string notice = "";
        private MessageType noticeType;
        private float contentWidth;

        // Scene preview
        private int previewLanguage = 1;

        // Connect
        private int scope, rowFilter;
        private string folder = "Assets";
        private bool scenes, prefabs = true;
        private LabelAlignment alignment;
        private List<ScanRow> rows = new List<ScanRow>();

        // Translate
        private int language, translateFilter;
        private TranslationImport import;
        private string editingKey, sourceDraft;
        private bool smartDraft, showAddKey;
        private string newKey = "", newSource = "";

        // Script texts
        private sealed class Phrase { public string text; public bool selected, registered; }
        private string scriptFolder = "Assets";
        private int scriptView;
        private List<Phrase> phrases = new List<Phrase>();
        private List<string> findings = new List<string>();

        // Dashboard
        private bool showInactive;
        private UnityEditor.Editor profileEditor;

        private sealed class Stats
        {
            public int total;
            public readonly Dictionary<string, int> done = new Dictionary<string, int>();
            public readonly Dictionary<string, int> review = new Dictionary<string, int>();
            public readonly Dictionary<long, string> hashes = new Dictionary<long, string>();
            public readonly Dictionary<string, string> reviewed = new Dictionary<string, string>();
            public int Done(string code) => done.TryGetValue(code, out int v) ? v : 0;
            public int Review(string code) => review.TryGetValue(code, out int v) ? v : 0;
            public int Missing(string code) => total - Done(code);
            public float Progress(string code) => total == 0 ? 0 : Done(code) / (float)total;
            public bool NeedsReview(string code, SharedTableData.SharedTableEntry entry, string value) =>
                !string.IsNullOrWhiteSpace(value) && (!reviewed.TryGetValue(code + "\n" + entry.Key, out var hash) ||
                !hashes.TryGetValue(entry.Id, out var source) || hash != source);
        }
        private Stats stats;
        private bool statsDirty = true;

        // ---------- Styles ----------
        private static readonly Color Accent = new Color(0.27f, 0.79f, 0.68f);
        private static readonly Color Warn = new Color(0.96f, 0.69f, 0.26f);
        private static readonly Color Danger = new Color(0.93f, 0.40f, 0.40f);
        private static readonly Color Info = new Color(0.40f, 0.62f, 0.95f);
        private static bool Pro => EditorGUIUtility.isProSkin;
        private static Color SidebarBg => Pro ? new Color(.13f, .135f, .145f) : new Color(.72f, .72f, .73f);
        private static Color CardBg => Pro ? new Color(.205f, .21f, .22f) : new Color(.86f, .86f, .87f);
        private static Color Hover => Pro ? new Color(1, 1, 1, .04f) : new Color(0, 0, 0, .05f);
        private static Color Selected => Pro ? new Color(1, 1, 1, .08f) : new Color(1, 1, 1, .35f);
        private static Color Track => Pro ? new Color(1, 1, 1, .08f) : new Color(0, 0, 0, .12f);
        private static Color Muted => Pro ? new Color(.60f, .63f, .67f) : new Color(.32f, .33f, .35f);
        private GUIStyle title, subtitle, cardStyle, section, muted, mutedWrap, nav, navSelected, navHint, badge, pill, big, wrap, bold, area;

        [MenuItem("Tools/RTL Localization Studio/Open Studio", priority = 1)]
        public static void Open()
        {
            var window = GetWindow<LocalizationStudio>();
            window.titleContent = new GUIContent("Localization Studio", EditorGUIUtility.IconContent("TextAsset Icon").image);
            window.minSize = new Vector2(880, 560);
        }
        private void OnEnable()
        {
            wantsMouseMove = true;
            searchField = new SearchField();
            Undo.undoRedoPerformed += OnUndo;
        }
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            LocalizationScenePreview.Stop();
            if (profileEditor) DestroyImmediate(profileEditor);
        }
        private void OnFocus() => statsDirty = true;
        private void OnUndo() { statsDirty = true; Repaint(); }

        private void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20 };
            subtitle = new GUIStyle(EditorStyles.label) { wordWrap = true, normal = { textColor = Muted } };
            cardStyle = new GUIStyle { padding = new RectOffset(16, 16, 12, 14), margin = new RectOffset(0, 0, 0, 10) };
            section = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Muted } };
            muted = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Muted }, clipping = TextClipping.Clip };
            mutedWrap = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = Muted } };
            nav = new GUIStyle(EditorStyles.label) { fontSize = 13 };
            navSelected = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            navHint = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Muted } };
            badge = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(6, 6, 1, 1), normal = { textColor = Color.black } };
            pill = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(7, 7, 1, 2) };
            big = new GUIStyle(EditorStyles.boldLabel) { fontSize = 24 };
            wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
            bold = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
            area = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        }

        // ---------- Frame ----------
        private void OnGUI()
        {
            Styles();
            if (Event.current.type == EventType.MouseMove) Repaint();
            contentWidth = position.width - SidebarWidth - Pad * 2 - 14;
            using (new EditorGUILayout.HorizontalScope())
            {
                Sidebar();
                using (new EditorGUILayout.VerticalScope(new GUIStyle { padding = new RectOffset((int)Pad, (int)Pad, 16, 8) }))
                {
                    GUILayout.Label(PageNames[(int)page], title);
                    GUILayout.Label(PageDescriptions[(int)page], subtitle);
                    GUILayout.Space(10);
                    NoticeBar();
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    { EmptyState("Play Mode is running", "Exit Play Mode to edit localization assets and bindings."); return; }
                    try
                    {
                        bool ready = LocalizationWorkspace.Collection;
                        if (!ready && page != Page.Help && page != Page.Settings) SetupState();
                        else switch (page)
                        {
                            case Page.Dashboard: Dashboard(); break;
                            case Page.Connect: Connect(); break;
                            case Page.Translate: Translate(); break;
                            case Page.Scripts: Scripts(); break;
                            case Page.Fonts: fontPanel.Draw(); break;
                            case Page.Settings: Settings(); break;
                            default: Help(); break;
                        }
                    }
                    catch (ExitGUIException) { throw; }
                    catch (Exception ex) { SetNotice(ex.Message, MessageType.Error); Debug.LogException(ex); }
                }
            }
        }

        private void Sidebar()
        {
            var bounds = EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(bounds, SidebarBg);
                EditorGUI.DrawRect(new Rect(bounds.xMax - 1, bounds.y, 1, bounds.height), Pro ? new Color(0, 0, 0, .4f) : new Color(0, 0, 0, .15f));
            }
            GUILayout.Space(16);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("RTL", section);
                    GUILayout.Label("Localization", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 });
                    bool ready = LocalizationWorkspace.Collection;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Dot(ready ? Accent : Warn);
                        GUILayout.Label(ready ? "Workspace ready" : "Setup needed", muted);
                    }
                }
            }
            GUILayout.Space(14);
            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            var s = LocalizationWorkspace.Collection ? GetStats() : null;
            for (int i = 0; i < PageNames.Length; i++)
            {
                if (i == (int)Page.Settings) GUILayout.Space(12);
                string count = null; Color color = Accent;
                if (s != null && i == (int)Page.Translate)
                {
                    int missing = ActiveCodes(s).Sum(s.Missing);
                    if (missing > 0) { count = missing.ToString(); color = Warn; }
                }
                if (i == (int)Page.Connect && rows.Count > 0)
                {
                    int ready = rows.Count(x => x.status == "Ready");
                    if (ready > 0) count = ready.ToString();
                }
                NavItem((Page)i, count, color);
            }
            EditorGUILayout.EndScrollView();
            if (LocalizationWorkspace.Collection) PreviewPanel();
            GUILayout.Space(12);
            EditorGUILayout.EndVertical();
        }

        private void NavItem(Page target, string count, Color badgeColor)
        {
            var r = GUILayoutUtility.GetRect(SidebarWidth, 44, GUILayout.ExpandWidth(true));
            bool selected = page == target;
            if (Event.current.type == EventType.Repaint)
            {
                if (selected) { EditorGUI.DrawRect(r, Selected); EditorGUI.DrawRect(new Rect(r.x, r.y + 6, 3, r.height - 12), Accent); }
                else if (r.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(r, Hover);
            }
            GUI.Label(new Rect(r.x + 18, r.y + 5, r.width - 70, 18), PageNames[(int)target], selected ? navSelected : nav);
            GUI.Label(new Rect(r.x + 18, r.y + 23, r.width - 30, 15), PageHints[(int)target], navHint);
            if (count != null)
            {
                var content = new GUIContent(count);
                float w = Mathf.Max(22, badge.CalcSize(content).x);
                var b = new Rect(r.xMax - w - 14, r.y + 13, w, 17);
                if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(b, badgeColor);
                GUI.Label(b, content, badge);
            }
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Go(target);
        }

        private void PreviewPanel()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(14);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("SCENE PREVIEW", section);
                    previewLanguage = Mathf.Clamp(previewLanguage, 0, LanguageCatalog.Names.Length - 1);
                    previewLanguage = EditorGUILayout.Popup(previewLanguage, LanguageCatalog.Names);
                    string code = LocalizationWorkspace.Codes[previewLanguage];
                    bool active = LocalizationScenePreview.IsActive;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool showing = active && LocalizationScenePreview.Language == code;
                        if (Button(showing ? "Refresh" : "Preview", true, 24))
                        { LocalizationScenePreview.Show(code); ClearNotice(); }
                        using (new EditorGUI.DisabledScope(!active))
                            if (GUILayout.Button("Restore", GUILayout.Height(24), GUILayout.Width(66))) LocalizationScenePreview.Stop();
                    }
                    string status = active
                        ? LanguageCatalog.Name(LocalizationScenePreview.Language) + " · " + LocalizationScenePreview.LabelCount + " labels · " + LocalizationScenePreview.FallbackCount + " fallbacks"
                        : "Shows connected texts in open scenes without entering Play Mode.";
                    GUILayout.Label(status, mutedWrap);
                    if (active && LocalizationScenePreview.UnformattedCount > 0)
                        GUILayout.Label(LocalizationScenePreview.UnformattedCount + " templates keep their placeholders.", mutedWrap);
                }
                GUILayout.Space(14);
            }
        }

        private void Go(Page target)
        {
            if (page == target) return;
            page = target; listPage = 0; search = ""; scroll = Vector2.zero;
            ClearNotice(); GUI.FocusControl(null);
        }

        // ---------- Dashboard ----------
        private void Dashboard()
        {
            var s = GetStats();
            var active = ActiveCodes(s).OrderByDescending(s.Progress).ToList();
            int missing = active.Sum(s.Missing), review = active.Sum(s.Review);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            NextStep(s, active, missing, review);
            using (new EditorGUILayout.HorizontalScope())
            {
                Metric("SOURCE TEXTS", s.total.ToString(), "Persian keys in table “" + LocalizationWorkspace.Profile.table + "”", null);
                GUILayout.Space(10);
                Metric("ACTIVE LANGUAGES", active.Count + " / " + TargetCodes.Length, "Languages with at least one translation", null);
                GUILayout.Space(10);
                Metric("MISSING", missing.ToString(), "Untranslated texts in active languages", missing > 0 ? Warn : Accent);
                GUILayout.Space(10);
                Metric("NEEDS REVIEW", review.ToString(), "Persian source changed or not reviewed", review > 0 ? Warn : Accent);
            }
            using (Card())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Languages", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(64))) statsDirty = true;
                }
                GUILayout.Space(4);
                if (active.Count == 0) GUILayout.Label("No translations yet. Pick a language below to start.", mutedWrap);
                foreach (string code in active) LanguageRow(code, s);
                var inactive = TargetCodes.Except(active).ToList();
                if (inactive.Count > 0)
                {
                    GUILayout.Space(6);
                    showInactive = EditorGUILayout.Foldout(showInactive, inactive.Count + " languages not started", true);
                    if (showInactive)
                    {
                        int columns = Mathf.Max(1, (int)(contentWidth / 190));
                        for (int i = 0; i < inactive.Count; i += columns)
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                foreach (string code in inactive.Skip(i).Take(columns))
                                    if (GUILayout.Button(new GUIContent(LanguageCatalog.Name(code), "Start translating " + code), GUILayout.Width(180)))
                                        OpenTranslate(code, 1);
                                GUILayout.FlexibleSpace();
                            }
                    }
                }
            }
            using (Card())
            {
                GUILayout.Label("Quick actions", EditorStyles.boldLabel);
                GUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Export CSV…", GUILayout.Height(26))) ExportCsv();
                    if (GUILayout.Button("Import CSV…", GUILayout.Height(26))) { page = Page.Translate; ImportCsv(); }
                    if (GUILayout.Button("Scan open scenes", GUILayout.Height(26))) { page = Page.Connect; scope = 0; Scan(); }
                    if (GUILayout.Button("Run validation", GUILayout.Height(26))) RunValidation();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void NextStep(Stats s, List<string> active, int missing, int review)
        {
            string heading, body, action; Action run;
            if (s.total == 0)
            {
                heading = "Connect your first texts";
                body = "Scan an open scene or a prefab folder and connect its texts. Each connected text becomes a Persian source key.";
                action = "Go to Connect texts"; run = () => Go(Page.Connect);
            }
            else if (active.Count == 0)
            {
                heading = "Start your first translation";
                body = s.total + " Persian texts are ready. Choose a language and translate here, or export a CSV for a translator.";
                action = "Translate English"; run = () => OpenTranslate("en", 1);
            }
            else if (missing > 0)
            {
                string code = active.Where(x => s.Missing(x) > 0).OrderBy(s.Missing).First();
                heading = LanguageCatalog.Name(code) + " is " + Mathf.FloorToInt(s.Progress(code) * 100) + "% done";
                body = s.Missing(code) + " texts still need a " + LanguageCatalog.Name(code) + " translation. Missing texts show the Persian source in the game.";
                action = "Translate missing"; run = () => OpenTranslate(code, 1);
            }
            else if (review > 0)
            {
                string code = active.OrderByDescending(s.Review).First();
                heading = review + " translations need review";
                body = "Their Persian source changed, or they were never marked reviewed. Check them and mark them reviewed.";
                action = "Review " + LanguageCatalog.Name(code); run = () => OpenTranslate(code, 2);
            }
            else
            {
                heading = "All active languages are complete";
                body = "Preview a language in the scene to check fonts, alignment and layout.";
                action = "Preview " + LanguageCatalog.Name(active[0]);
                string code = active[0];
                run = () => { previewLanguage = Array.IndexOf(LocalizationWorkspace.Codes, code); LocalizationScenePreview.Show(code); };
            }
            var r = EditorGUILayout.BeginVertical(cardStyle);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, CardBg);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 4, r.height), Accent);
            }
            GUILayout.Label("NEXT STEP", section);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(heading, new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 });
                    GUILayout.Label(body, subtitle);
                }
                GUILayout.Space(12);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(190)))
                {
                    GUILayout.Space(6);
                    if (Button(action, true, 32)) run();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void LanguageRow(string code, Stats s)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(26)))
            {
                GUILayout.Label(new GUIContent(LanguageCatalog.Name(code), code), GUILayout.Width(170));
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(9);
                    Bar(s.Progress(code), s.Missing(code) == 0 ? Accent : Info);
                }
                GUILayout.Label(s.Done(code) + " / " + s.total, muted, GUILayout.Width(80));
                if (s.Review(code) > 0) Pill(s.Review(code) + " review", Warn, 82); else GUILayout.Space(86);
                if (GUILayout.Button(s.Missing(code) > 0 ? "Translate" : "Open", EditorStyles.miniButtonLeft, GUILayout.Width(70)))
                    OpenTranslate(code, s.Missing(code) > 0 ? 1 : 0);
                if (GUILayout.Button("Preview", EditorStyles.miniButtonRight, GUILayout.Width(60)))
                { previewLanguage = Array.IndexOf(LocalizationWorkspace.Codes, code); LocalizationScenePreview.Show(code); }
            }
        }

        private void OpenTranslate(string code, int filter)
        {
            Go(Page.Translate);
            language = Mathf.Max(0, Array.IndexOf(TargetCodes, code));
            translateFilter = filter;
            listPage = 0;
        }

        // ---------- Connect ----------
        private static int Group(string status) => status == "Ready" ? 1 : status == "Connected" ? 3 : status == "Excluded" ? 4 : 2;
        private static readonly string[] GroupNames = { "All", "Ready", "Needs review", "Connected", "Excluded" };
        private static Color GroupColor(int group) => group == 1 ? Accent : group == 2 ? Warn : group == 3 ? Info : Muted;

        private void Connect()
        {
            using (Card())
            {
                GUILayout.Label("1  Choose what to scan", EditorStyles.boldLabel);
                GUILayout.Space(4);
                scope = GUILayout.Toolbar(scope, new[] { "Open scenes", "Selected objects", "Asset folder" }, GUILayout.Height(24));
                GUILayout.Space(6);
                if (scope == 0) GUILayout.Label("Every TMP text in the loaded scenes, including inactive objects.", mutedWrap);
                else if (scope == 1) GUILayout.Label(Selection.gameObjects.Length + " objects selected in the Hierarchy. Their children are included.", mutedWrap);
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        folder = EditorGUILayout.TextField(folder);
                        if (GUILayout.Button("Browse…", GUILayout.Width(80))) { folder = BrowseFolder(folder); GUIUtility.ExitGUI(); }
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        prefabs = EditorGUILayout.ToggleLeft("Prefabs", prefabs, GUILayout.Width(90));
                        scenes = EditorGUILayout.ToggleLeft("Scenes", scenes, GUILayout.Width(90));
                        GUILayout.Label("Tip: scan prefab folders before scenes.", muted);
                    }
                }
                GUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (Button("Scan texts", true, 28, 160)) Scan();
                }
            }
            if (rows.Count == 0) { EmptyState("Nothing scanned yet", "Choose a scope above and press Scan texts. Scanning never changes your assets."); return; }

            var counts = new int[5];
            counts[0] = rows.Count;
            foreach (var row in rows) counts[Group(row.status)]++;
            using (new EditorGUILayout.HorizontalScope())
            {
                rowFilter = GUILayout.Toolbar(rowFilter, GroupNames.Select((x, i) => x + "  " + counts[i]).ToArray(), GUILayout.Height(22), GUILayout.MaxWidth(560));
                GUILayout.Space(8);
                Search();
            }
            var filtered = rows.Where(x => (rowFilter == 0 || Group(x.status) == rowFilter) && Matches(x.text, x.key, x.hierarchy, x.status)).ToList();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select all ready", EditorStyles.miniButtonLeft)) rows.ForEach(x => x.selected = x.status == "Ready");
                if (GUILayout.Button("Select shown", EditorStyles.miniButtonMid)) filtered.ForEach(x => x.selected = Group(x.status) <= 2);
                if (GUILayout.Button("Clear selection", EditorStyles.miniButtonRight)) rows.ForEach(x => x.selected = false);
                GUILayout.Space(8);
                if (GUILayout.Button(new GUIContent("Share keys for identical text", "Selected rows with exactly the same text get one key. Only do this when the meaning is identical."), EditorStyles.miniButton))
                    foreach (var group in rows.Where(x => x.selected).GroupBy(x => x.text))
                    { string key = group.First().key; foreach (var row in group) row.key = key; }
                GUILayout.FlexibleSpace();
                Pagination(filtered.Count);
            }
            GUILayout.Space(4);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            float textWidth = Mathf.Max(160, contentWidth - 18 - 112 - 250 - 60 - 40);
            foreach (var row in filtered.Skip(listPage * PageSize).Take(PageSize))
                using (Card(8))
                using (new EditorGUILayout.HorizontalScope())
                {
                    int group = Group(row.status);
                    using (new EditorGUI.DisabledScope(group >= 3))
                        row.selected = EditorGUILayout.Toggle(row.selected, GUILayout.Width(18));
                    Pill(group == 2 ? ShortStatus(row.status) : row.status, GroupColor(group), 104);
                    GUILayout.Space(6);
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(textWidth)))
                    {
                        GUILayout.Label(string.IsNullOrWhiteSpace(row.text) ? "(empty)" : row.text, wrap, GUILayout.Width(textWidth));
                        GUILayout.Label(new GUIContent(ShortPath(row.hierarchy), row.assetPath + "\n" + row.hierarchy), muted, GUILayout.Width(textWidth));
                    }
                    using (new EditorGUI.DisabledScope(group >= 3))
                        row.key = EditorGUILayout.TextField(row.key, GUILayout.Width(240));
                    using (new EditorGUI.DisabledScope(!row.target))
                        if (GUILayout.Button("Locate", EditorStyles.miniButton, GUILayout.Width(54)))
                        { Selection.activeObject = row.target; EditorGUIUtility.PingObject(row.target); }
                }
            EditorGUILayout.EndScrollView();
            ConnectActions();
        }

        private void ConnectActions()
        {
            int selected = rows.Count(x => x.selected);
            var r = EditorGUILayout.BeginVertical(cardStyle);
            if (Event.current.type == EventType.Repaint) { EditorGUI.DrawRect(r, CardBg); EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), Accent); }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(selected + " selected", EditorStyles.boldLabel, GUILayout.Width(100));
                GUILayout.Label("Alignment", GUILayout.Width(64));
                alignment = (LabelAlignment)EditorGUILayout.EnumPopup(alignment, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(selected == 0))
                {
                    if (GUILayout.Button(new GUIContent("Exclude", "Mark as user content / never translate."), GUILayout.Height(28), GUILayout.Width(80)))
                    { SetNotice(LocalizationScanner.Apply(rows, alignment, true) + " texts excluded. Save changed scenes.", MessageType.Info); }
                    if (GUILayout.Button(new GUIContent("Auto-translate only", "For empty or script-filled labels: Persian text written with .text / SetText is translated if it exists in the table."), GUILayout.Height(28), GUILayout.Width(140)))
                    { SetNotice(LocalizationScanner.Apply(rows, alignment, autoOnly: true) + " text watchers attached. Save changed scenes.", MessageType.Info); statsDirty = true; }
                    if (Button("Connect " + selected, true, 28, 130))
                    { SetNotice(LocalizationScanner.Apply(rows, alignment) + " texts connected. Save your loaded scenes when ready.", MessageType.Info); statsDirty = true; }
                }
            }
            GUILayout.Label("Loaded scenes support Undo. Saved prefabs and closed scenes are backed up to Library/RTLLocalizationStudioBackups first.", mutedWrap);
            EditorGUILayout.EndVertical();
        }

        private void Scan()
        {
            if (scope == 2 && !AssetDatabase.IsValidFolder(folder)) throw new ArgumentException("Choose a valid folder inside Assets.");
            rows = scope == 2 ? LocalizationScanner.ScanAssets(folder, scenes, prefabs) : LocalizationScanner.ScanLoaded(scope == 1);
            listPage = 0; rowFilter = 0; search = "";
            SetNotice(rows.Count + " texts found, " + rows.Count(x => x.status == "Ready") + " ready to connect. Review the selection, then press Connect.", MessageType.Info);
        }
        private static string ShortStatus(string status) => status.Replace(": review", "");
        private static string ShortPath(string hierarchy)
        {
            var parts = Regex.Replace(hierarchy ?? "", @"\[\d+\]", "").Split('/');
            return parts.Length <= 3 ? string.Join(" / ", parts) : "… / " + string.Join(" / ", parts.Skip(parts.Length - 3));
        }

        // ---------- Translate ----------
        private void Translate()
        {
            var s = GetStats();
            var collection = LocalizationWorkspace.Collection;
            language = Mathf.Clamp(language, 0, TargetCodes.Length - 1);
            string code = TargetCodes[language];
            var faTable = LocalizationWorkspace.Table("fa");
            var table = LocalizationWorkspace.Table(code);
            string Fa(long id) => faTable ? faTable.GetEntry(id)?.Value ?? "" : "";
            string Tr(long id) => table ? table.GetEntry(id)?.Value ?? "" : "";

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Language", GUILayout.Width(62));
                var names = TargetCodes.Select(x => LanguageCatalog.Name(x) + "   " + Mathf.FloorToInt(s.Progress(x) * 100) + "%").ToArray();
                int next = EditorGUILayout.Popup(language, names, GUILayout.Width(250), GUILayout.Height(20));
                if (next != language) { language = next; listPage = 0; editingKey = null; }
                GUILayout.Space(10);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(160)))
                {
                    GUILayout.Space(7);
                    Bar(s.Progress(code), s.Missing(code) == 0 ? Accent : Info);
                }
                GUILayout.Label(s.Done(code) + " / " + s.total, muted, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Export CSV…", GUILayout.Height(22))) ExportCsv();
                if (GUILayout.Button("Import CSV…", GUILayout.Height(22))) ImportCsv();
                if (GUILayout.Button("Save", GUILayout.Height(22), GUILayout.Width(50))) { AssetDatabase.SaveAssets(); SetNotice("Tables saved.", MessageType.Info); }
            }
            GUILayout.Space(6);
            if (import != null) ImportPreview();

            using (new EditorGUILayout.HorizontalScope())
            {
                string[] filters = { "All  " + s.total, "Missing  " + s.Missing(code), "Needs review  " + s.Review(code) };
                int next = GUILayout.Toolbar(translateFilter, filters, GUILayout.Height(22), GUILayout.MaxWidth(420));
                if (next != translateFilter) { translateFilter = next; listPage = 0; }
                GUILayout.Space(8);
                Search();
            }
            var entries = collection.SharedData.Entries.Where(e =>
            {
                string value = Tr(e.Id);
                if (translateFilter == 1 && !string.IsNullOrWhiteSpace(value)) return false;
                if (translateFilter == 2 && !s.NeedsReview(code, e, value)) return false;
                return Matches(e.Key, Fa(e.Id), value);
            }).ToList();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                Pagination(entries.Count);
            }
            GUILayout.Space(4);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (entries.Count == 0)
                EmptyState(translateFilter == 1 ? "Nothing missing" : translateFilter == 2 ? "Nothing to review" : "No matching texts",
                    translateFilter > 0 ? "Every " + LanguageCatalog.Name(code) + " text in this view is done." : "Try a different search.");
            float column = Mathf.Max(200, (contentWidth - 32 - 14) / 2);
            foreach (var entry in entries.Skip(listPage * PageSize).Take(PageSize))
                TranslationRow(entry, code, Fa(entry.Id), Tr(entry.Id), column, s);
            EditorGUILayout.EndScrollView();
            AddKeyCard();
        }

        private void TranslationRow(SharedTableData.SharedTableEntry entry, string code, string fa, string old, float column, Stats s)
        {
            bool smart = LocalizationWorkspace.Table("fa")?.GetEntry(entry.Id)?.IsSmart ?? false;
            bool review = s.NeedsReview(code, entry, old);
            using (Card(10))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(entry.Key, muted, GUILayout.Height(16));
                    GUILayout.FlexibleSpace();
                    if (smart) Pill("Smart", Info, 52);
                    if (string.IsNullOrWhiteSpace(old)) Pill("Missing", Danger, 62);
                    else if (review) Pill("Review", Warn, 58);
                }
                GUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(column)))
                    {
                        GUILayout.Label("PERSIAN SOURCE", section);
                        if (editingKey == entry.Key)
                        {
                            sourceDraft = EditorGUILayout.TextArea(sourceDraft, area, GUILayout.MinHeight(38), GUILayout.Width(column));
                            smartDraft = EditorGUILayout.ToggleLeft(new GUIContent("Smart parameters", "Use named placeholders such as {score}."), smartDraft);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (Button("Save", true, 20, 70)) SaveSource(entry);
                                if (GUILayout.Button("Cancel", GUILayout.Width(70))) editingKey = null;
                            }
                        }
                        else
                        {
                            EditorGUILayout.SelectableLabel(fa, wrap, GUILayout.Width(column), GUILayout.Height(Mathf.Max(20, wrap.CalcHeight(new GUIContent(fa), column))));
                            if (GUILayout.Button("Edit source", EditorStyles.miniButton, GUILayout.Width(80)))
                            { editingKey = entry.Key; sourceDraft = fa; smartDraft = smart; GUI.FocusControl(null); }
                        }
                    }
                    GUILayout.Space(14);
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(column)))
                    {
                        GUILayout.Label(LanguageCatalog.Name(code).ToUpperInvariant(), section);
                        string value = EditorGUILayout.TextArea(old, area, GUILayout.MinHeight(38), GUILayout.Width(column));
                        if (value != old) { LocalizationWorkspace.SetValue(entry.Key, code, value, smart); statsDirty = true; }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (review && GUILayout.Button("Mark reviewed", EditorStyles.miniButtonLeft, GUILayout.Width(100)))
                            { TranslationReviewState.Get().MarkReviewed(entry.Key, code); statsDirty = true; }
                            if (GUILayout.Button(new GUIContent("Preview on selected text", "Select a TMP text in the Hierarchy to see this translation with its font and style."),
                                review ? EditorStyles.miniButtonRight : EditorStyles.miniButton, GUILayout.Width(150)))
                            {
                                var text = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<TMP_Text>() : null;
                                if (text) LocalizationTextPreview.Open(text, string.IsNullOrEmpty(value) ? fa : value, code);
                                else SetNotice("Select a TMP text in the Hierarchy first.", MessageType.Warning);
                            }
                        }
                    }
                }
            }
        }

        private void SaveSource(SharedTableData.SharedTableEntry entry)
        {
            LocalizationWorkspace.SetValue(entry.Key, "fa", sourceDraft, smartDraft);
            foreach (string other in TargetCodes)
            {
                var table = LocalizationWorkspace.Table(other);
                if (table && table.GetEntry(entry.Id) != null)
                    LocalizationWorkspace.SetValue(entry.Key, other, table.GetEntry(entry.Id).Value, smartDraft);
            }
            editingKey = null; statsDirty = true;
            GUI.FocusControl(null);
        }

        private void AddKeyCard()
        {
            using (Card())
            {
                showAddKey = EditorGUILayout.Foldout(showAddKey, "Add a new text", true);
                if (!showAddKey) return;
                newKey = EditorGUILayout.TextField(new GUIContent("Key", "For example menu.play or game.score"), newKey);
                newSource = EditorGUILayout.TextField("Persian source", newSource);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newKey) || string.IsNullOrWhiteSpace(newSource)))
                        if (Button("Add text", true, 24, 110))
                        {
                            if (LocalizationWorkspace.Collection.SharedData.GetEntry(newKey) != null) SetNotice("The key “" + newKey + "” already exists.", MessageType.Warning);
                            else
                            {
                                LocalizationWorkspace.SetValue(newKey.Trim(), "fa", newSource); AssetDatabase.SaveAssets();
                                SetNotice("Added “" + newKey + "”.", MessageType.Info); newKey = newSource = ""; statsDirty = true; GUI.FocusControl(null);
                            }
                        }
                }
            }
        }

        private void ImportPreview()
        {
            using (Card())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("CSV import preview", EditorStyles.boldLabel);
                    Pill(import.changes.Count + " changes", Accent, 90);
                    if (import.errors.Count > 0) Pill(import.errors.Count + " errors", Danger, 80);
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Label("Nothing has been changed yet. Review, then apply.", mutedWrap);
                foreach (var error in import.errors.Take(5)) EditorGUILayout.HelpBox(error, MessageType.Error);
                if (import.errors.Count > 5) GUILayout.Label("…and " + (import.errors.Count - 5) + " more errors. Save the full report to see all.", mutedWrap);
                foreach (var change in import.changes.Take(6))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(change.key + "  [" + change.language + "]", muted, GUILayout.Width(Mathf.Min(260, contentWidth * .3f)));
                        GUILayout.Label((string.IsNullOrEmpty(change.before) ? "(empty)" : change.before) + "   →   " + change.after, wrap);
                    }
                if (import.changes.Count > 6) GUILayout.Label("…and " + (import.changes.Count - 6) + " more changes.", mutedWrap);
                GUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Save full report…"))
                    {
                        string path = EditorUtility.SaveFilePanel("Import report", "", "import-review.txt", "txt");
                        if (path.Length > 0) File.WriteAllLines(path, import.errors.Concat(import.changes.Select(x => x.key + " [" + x.language + "]\nBEFORE: " + x.before + "\nAFTER: " + x.after)));
                        GUIUtility.ExitGUI();
                    }
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Discard", GUILayout.Width(80))) import = null;
                    using (new EditorGUI.DisabledScope(import == null || import.errors.Count > 0 || import.changes.Count == 0))
                        if (Button("Apply " + (import?.changes.Count ?? 0) + " changes", true, 22, 150))
                        {
                            TranslationExchange.Apply(import); import = null; statsDirty = true;
                            SetNotice("Translations imported.", MessageType.Info);
                        }
                }
            }
        }

        private void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("Export translations", "", "translations.csv", "csv");
            if (path.Length > 0)
            {
                TranslationExchange.Export(path);
                SetNotice("CSV exported. Translators should only edit language columns; keep Key, Id, fa, Smart and SourceHash unchanged.", MessageType.Info);
            }
            GUIUtility.ExitGUI();
        }
        private void ImportCsv()
        {
            string path = EditorUtility.OpenFilePanel("Import translations", "", "csv");
            if (path.Length > 0) { import = TranslationExchange.Preview(File.ReadAllText(path)); ClearNotice(); }
            GUIUtility.ExitGUI();
        }

        // ---------- Script texts ----------
        private void Scripts()
        {
            using (Card())
            {
                GUILayout.Label("Scan your C# scripts", EditorStyles.boldLabel);
                GUILayout.Label("Keep using .text and SetText in code. Register their Persian strings here, translate them, and enable Auto-translate on the labels that show them. Exclude user names and chat.", mutedWrap);
                GUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    scriptFolder = EditorGUILayout.TextField(scriptFolder);
                    if (GUILayout.Button("Browse…", GUILayout.Width(80))) { scriptFolder = BrowseFolder(scriptFolder); GUIUtility.ExitGUI(); }
                }
                GUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("List .text / SetText lines", GUILayout.Height(28), GUILayout.Width(180))) { FindAssignments(); scriptView = 1; }
                    if (Button("Find Persian strings", true, 28, 170)) { FindPhrases(); scriptView = 0; }
                }
            }
            if (phrases.Count == 0 && findings.Count == 0) { EmptyState("Nothing scanned yet", "Pick your scripts folder and press Find Persian strings."); return; }
            using (new EditorGUILayout.HorizontalScope())
            {
                scriptView = GUILayout.Toolbar(scriptView, new[] { "Persian strings  " + phrases.Count, "Text assignments  " + findings.Count }, GUILayout.Height(22), GUILayout.MaxWidth(360));
                GUILayout.Space(8);
                Search();
            }
            GUILayout.Space(4);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (scriptView == 0)
            {
                foreach (var phrase in phrases.Where(x => Matches(x.text)))
                    using (Card(8))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(phrase.registered))
                            phrase.selected = EditorGUILayout.Toggle(phrase.selected && !phrase.registered, GUILayout.Width(18));
                        EditorGUILayout.SelectableLabel(phrase.text, wrap, GUILayout.Height(Mathf.Max(20, wrap.CalcHeight(new GUIContent(phrase.text), contentWidth - 140))));
                        if (phrase.registered) Pill("In table", Info, 66);
                    }
            }
            else
            {
                foreach (string item in findings.Where(x => Matches(x)))
                    if (GUILayout.Button(item, wrap))
                    {
                        int colon = item.IndexOf(':', 2);
                        int end = item.IndexOf("  ", colon, StringComparison.Ordinal);
                        AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(item.Substring(0, colon)), int.Parse(item.Substring(colon + 1, end - colon - 1)));
                    }
            }
            EditorGUILayout.EndScrollView();
            if (scriptView == 0 && phrases.Count > 0)
            {
                int selected = phrases.Count(x => x.selected && !x.registered);
                using (Card())
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(selected + " selected · " + phrases.Count(x => x.registered) + " already in table", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Select none", GUILayout.Height(28), GUILayout.Width(90))) phrases.ForEach(x => x.selected = false);
                    using (new EditorGUI.DisabledScope(selected == 0))
                        if (Button("Add " + selected + " to table", true, 28, 160)) RegisterPhrases();
                }
            }
        }

        private void FindPhrases()
        {
            if (!AssetDatabase.IsValidFolder(scriptFolder)) throw new ArgumentException("Choose a valid folder inside Assets.");
            var existing = new HashSet<string>(LocalizationWorkspace.Table("fa").Values.Select(x => x.Value).Where(x => x != null));
            phrases = AssetDatabase.FindAssets("t:MonoScript", new[] { scriptFolder })
                .Select(AssetDatabase.GUIDToAssetPath).SelectMany(path => ScriptSourceExtractor.Extract(File.ReadAllText(path)))
                .Where(value => value.Any(c => c >= '؀' && c <= 'ۿ')).Distinct()
                .Select(x => new Phrase { text = x, registered = existing.Contains(x), selected = !existing.Contains(x) })
                .OrderBy(x => x.registered).ToList();
            SetNotice(phrases.Count + " Persian strings found, " + phrases.Count(x => !x.registered) + " new. Untick anything that should not be translated.", MessageType.Info);
        }
        private void FindAssignments()
        {
            if (!AssetDatabase.IsValidFolder(scriptFolder)) throw new ArgumentException("Choose a valid folder inside Assets.");
            findings.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { scriptFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                    if (Regex.IsMatch(lines[i], @"\.text\s*=|\.SetText\s*\(")) findings.Add(path + ":" + (i + 1) + "  " + lines[i].Trim());
            }
            SetNotice(findings.Count + " possible text assignments. Click one to open it in your code editor.", MessageType.Info);
        }
        private void RegisterPhrases()
        {
            int count = 0;
            foreach (var phrase in phrases.Where(x => x.selected && !x.registered))
            {
                LocalizationWorkspace.SetValue("code." + LocalizationWorkspace.SourceHash(phrase.text).ToLowerInvariant(), "fa", phrase.text);
                phrase.registered = true; phrase.selected = false; count++;
            }
            AssetDatabase.SaveAssets(); statsDirty = true;
            SetNotice(count + " strings added. Translate them in Translate or through CSV.", MessageType.Info);
        }

        // ---------- Settings & help ----------
        private void Settings()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (Card())
            {
                GUILayout.Label("Workspace", EditorStyles.boldLabel);
                bool ready = LocalizationWorkspace.Collection;
                GUILayout.Label(ready
                    ? "Locales, the “" + LocalizationWorkspace.Profile.table + "” table, Persian fallback and the saved language selector are configured. Repair is safe to run at any time; it keeps existing data."
                    : "Creates all " + LanguageCatalog.Codes.Length + " languages, the translation table, Persian fallback and saved language selection.", mutedWrap);
                GUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (Button(ready ? "Check / repair workspace" : "Set up localization", !ready, 28, 200))
                    { LocalizationWorkspace.Setup(); statsDirty = true; SetNotice("Workspace is ready.", MessageType.Info); }
                    if (GUILayout.Button("Run validation", GUILayout.Height(28), GUILayout.Width(130))) RunValidation();
                    if (GUILayout.Button("Open README", GUILayout.Height(28), GUILayout.Width(110)))
                        EditorUtility.RevealInFinder(Path.GetFullPath("Packages/com.hadimakhsoosi7.rtl-localization-studio/README.md"));
                }
            }
            if (LocalizationWorkspace.Profile)
                using (Card())
                {
                    GUILayout.Label("Language styles", EditorStyles.boldLabel);
                    GUILayout.Label("Per-language font, font scale, direction and digit style. Assign fonts that include the required glyphs. Alignment and digit conversion only change on labels that opt in.", mutedWrap);
                    GUILayout.Space(6);
                    UnityEditor.Editor.CreateCachedEditor(LocalizationWorkspace.Profile, null, ref profileEditor);
                    profileEditor.OnInspectorGUI();
                }
            EditorGUILayout.EndScrollView();
        }

        private void Help()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var steps = new[]
            {
                ("Connect texts", "Scan a prefab folder first, then scenes. Review the selection and press Connect. Each text becomes a Persian source key."),
                ("Script texts", "Find Persian strings in your C# code and add them to the table. Use Auto-translate only on labels filled from code."),
                ("Translate", "Translate inside the Studio, or export a CSV, send it to a translator and import it back with a preview."),
                ("Fonts", "Map each original font to a replacement per language so every script renders."),
                ("Verify", "Preview each language from the sidebar, then test on a real device: fa → en → ar → fa, popups, and restart.")
            };
            for (int i = 0; i < steps.Length; i++)
                using (Card())
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label((i + 1).ToString(), big, GUILayout.Width(34));
                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label(steps[i].Item1, EditorStyles.boldLabel);
                        GUILayout.Label(steps[i].Item2, mutedWrap);
                    }
                }
            using (Card())
            {
                GUILayout.Label("Runtime API", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(
                    "Localization.SetLanguage(\"en\");          // saved on the device\n" +
                    "label.SetKey(\"menu.play\");\n" +
                    "label.SetKey(\"game.score\", new { score = 120 });   // Smart entry\n" +
                    "label.SetArguments(new { score = 150 });\n" +
                    "title.text = \"<Persian source text>\";   // auto-translated if registered",
                    EditorStyles.textArea, GUILayout.Height(84));
                GUILayout.Label("UI buttons can call LanguageButton.SelectLanguage from OnClick.", mutedWrap);
            }
            using (Card())
            {
                GUILayout.Label("Good to know", EditorStyles.boldLabel);
                GUILayout.Label("• Alignment is preserved unless a label uses FollowLanguage.\n• Share a key between identical texts only when the meaning is identical.\n• Empty CSV cells keep existing translations.\n• CSV rows based on an outdated Persian source are rejected.\n• Use LocalizedLayout only on chosen rows and directional icons.", mutedWrap);
            }
            EditorGUILayout.EndScrollView();
        }

        private void SetupState()
        {
            using (Card())
            {
                GUILayout.Label("Set up localization", new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 });
                GUILayout.Label("One click creates all " + LanguageCatalog.Codes.Length + " languages, the translation table, Persian fallback and saved language selection. Existing settings are kept.", subtitle);
                GUILayout.Space(8);
                if (Button("Set up localization", true, 32, 200)) { LocalizationWorkspace.Setup(); statsDirty = true; SetNotice("Workspace is ready. Start with Connect texts.", MessageType.Info); }
            }
        }

        private void RunValidation()
        {
            LocalizationValidation.Run();
            SetNotice("Validation finished. Results: Logs/localization-validation.txt", MessageType.Info);
        }

        // ---------- Stats ----------
        private Stats GetStats()
        {
            if (stats != null && !statsDirty) return stats;
            statsDirty = false;
            var s = new Stats();
            var collection = LocalizationWorkspace.Collection;
            if (!collection) return stats = s;
            var entries = collection.SharedData.Entries;
            s.total = entries.Count;
            var faTable = collection.GetTable("fa") as StringTable;
            foreach (var entry in entries) s.hashes[entry.Id] = LocalizationWorkspace.SourceHash(faTable ? faTable.GetEntry(entry.Id)?.Value : "");
            foreach (var record in TranslationReviewState.Get().records) s.reviewed[record.language + "\n" + record.key] = record.sourceHash;
            foreach (string code in LocalizationWorkspace.Codes)
            {
                var table = collection.GetTable(code) as StringTable;
                int done = 0, review = 0;
                if (table)
                    foreach (var entry in entries)
                    {
                        string value = table.GetEntry(entry.Id)?.Value;
                        if (string.IsNullOrWhiteSpace(value)) continue;
                        done++;
                        if (code != "fa" && s.NeedsReview(code, entry, value)) review++;
                    }
                s.done[code] = done; s.review[code] = review;
            }
            return stats = s;
        }
        private static IEnumerable<string> ActiveCodes(Stats s) => TargetCodes.Where(x => s.Done(x) > 0);

        // ---------- Widgets ----------
        private sealed class CardScope : IDisposable
        {
            public CardScope(GUIStyle style, Color color)
            {
                var r = EditorGUILayout.BeginVertical(style);
                if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(r, color);
            }
            public void Dispose() => EditorGUILayout.EndVertical();
        }
        private IDisposable Card(int vertical = 12)
        {
            var style = vertical == 12 ? cardStyle : new GUIStyle(cardStyle) { padding = new RectOffset(12, 12, vertical, vertical), margin = new RectOffset(0, 0, 0, 6) };
            return new CardScope(style, CardBg);
        }
        private bool Button(string text, bool primary, float height, float width = 0)
        {
            var old = GUI.backgroundColor;
            if (primary) GUI.backgroundColor = Accent;
            bool clicked = width > 0
                ? GUILayout.Button(text, GUILayout.Height(height), GUILayout.Width(width))
                : GUILayout.Button(text, GUILayout.Height(height));
            GUI.backgroundColor = old;
            return clicked;
        }
        private void Metric(string label, string value, string hint, Color? color)
        {
            using (Card())
            {
                GUILayout.Label(label, section);
                var style = color.HasValue ? new GUIStyle(big) { normal = { textColor = color.Value } } : big;
                GUILayout.Label(value, style, GUILayout.Height(32));
                GUILayout.Label(hint, mutedWrap);
            }
        }
        private void Pill(string text, Color color, float width)
        {
            var r = GUILayoutUtility.GetRect(width, 18, GUILayout.Width(width), GUILayout.Height(18));
            r.y += 1;
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(r, new Color(color.r, color.g, color.b, .22f));
            var style = new GUIStyle(pill) { normal = { textColor = Pro ? color : Color.Lerp(color, Color.black, .45f) } };
            GUI.Label(r, text, style);
        }
        private static void Dot(Color color)
        {
            var r = GUILayoutUtility.GetRect(8, 8, GUILayout.Width(8), GUILayout.Height(14));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(new Rect(r.x, r.y + 4, 7, 7), color);
        }
        private static void Bar(float value, Color color)
        {
            var r = GUILayoutUtility.GetRect(40, 6, GUILayout.ExpandWidth(true), GUILayout.Height(6));
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(r, Track);
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value), r.height), color);
        }
        private void EmptyState(string heading, string body)
        {
            GUILayout.Space(30);
            GUILayout.Label(heading, new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 14 });
            GUILayout.Label(body, new GUIStyle(mutedWrap) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(30);
        }
        private void Search()
        {
            string next = searchField.OnGUI(search, GUILayout.MinWidth(140));
            if (next != search) { search = next; listPage = 0; }
        }
        private void SetNotice(string text, MessageType type) { notice = text; noticeType = type; }
        private void ClearNotice() => notice = "";
        private void NoticeBar()
        {
            if (string.IsNullOrEmpty(notice)) return;
            Color color = noticeType == MessageType.Error ? Danger : noticeType == MessageType.Warning ? Warn : Accent;
            var r = EditorGUILayout.BeginHorizontal(new GUIStyle { padding = new RectOffset(14, 8, 8, 8), margin = new RectOffset(0, 0, 0, 10) });
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, new Color(color.r, color.g, color.b, .14f));
                EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), color);
            }
            GUILayout.Label(notice, wrap);
            if (GUILayout.Button("×",EditorStyles.label, GUILayout.Width(18))) ClearNotice();
            EditorGUILayout.EndHorizontal();
        }
        private string BrowseFolder(string current)
        {
            string chosen = EditorUtility.OpenFolderPanel("Choose a folder inside Assets", current, "");
            if (string.IsNullOrEmpty(chosen)) return current;
            string root = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            chosen = Path.GetFullPath(chosen).Replace('\\', '/');
            if (chosen == root) return "Assets";
            if (chosen.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)) return "Assets" + chosen.Substring(root.Length);
            SetNotice("Choose a folder inside this project's Assets folder.", MessageType.Warning);
            return current;
        }
        private bool Matches(params string[] values) => string.IsNullOrEmpty(search) || values.Any(x => (x ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        private void Pagination(int count)
        {
            int pages = Math.Max(1, (count + PageSize - 1) / PageSize);
            listPage = Mathf.Clamp(listPage, 0, pages - 1);
            GUILayout.Label(count + " results", muted, GUILayout.Width(80));
            if (pages <= 1) return;
            using (new EditorGUI.DisabledScope(listPage == 0))
                if (GUILayout.Button("‹", EditorStyles.miniButtonLeft, GUILayout.Width(26))) { listPage--; scroll = Vector2.zero; }
            GUILayout.Label((listPage + 1) + " / " + pages, new GUIStyle(muted) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(52));
            using (new EditorGUI.DisabledScope(listPage >= pages - 1))
                if (GUILayout.Button("›", EditorStyles.miniButtonRight, GUILayout.Width(26))) { listPage++; scroll = Vector2.zero; }
        }
    }
}
