# RTL Localization Studio

Reusable Unity 6 package built on Unity Localization. Open **Tools > RTL Localization Studio > Open Studio**.

## نصب در هر پروژه

**از گیت‌هاب (پیشنهادی):** در `Packages/manifest.json` بخش `dependencies`:

```json
"com.hadimakhsoosi7.rtl-localization-studio": "https://github.com/hadimakhsoosi7/rtl-localization-studio.git#v1.0.0"
```

یا در Unity: **Window > Package Manager > + > Add package from git URL…** و همان آدرس. `#v1.0.0` نسخه را قفل می‌کند؛ برای به‌روزرسانی، تگ را عوض کنید.

**محلی:** پوشه را در `Packages/` پروژه کپی کنید (embedded) یا `"com.hadimakhsoosi7.rtl-localization-studio": "file:D:/path/to/com.hadimakhsoosi7.rtl-localization-studio"`.

بعد از نصب، راه‌اندازی اولیه خودکار انجام می‌شود (`Assets/Localization` و تنظیمات Localization ساخته می‌شوند).

**RTLTMPro (اختیاری):** اگر RTLTMPro در پروژه باشد، پل `RTL/` با define `RTL_LOCALIZATION_RTLTMPRO` خودکار فعال می‌شود. برای پشتیبانی کامل از `SetText`، تغییر `Documentation~/RTLTextMeshPro.patch` را روی `RTLTextMeshPro.cs` همان پروژه اعمال کنید (`git apply`).

## زبان‌ها و مدیریت فونت

۲۹ زبان آماده‌اند: فارسی `fa`، انگلیسی `en`، عربی `ar`، چینی ساده `zh-Hans`، کره‌ای `ko`، ژاپنی `ja`، فرانسوی `fr`، آلمانی `de`، اسپانیایی `es`، ایتالیایی `it`، هلندی `nl`، نروژی بوکمال `nb`، پرتغالی `pt`، ترکی `tr`، فنلاندی `fi`، روسی `ru`، چینی سنتی `zh-Hant`، مالایی `ms`، اندونزیایی `id`، تایلندی `th` ویتنامی `vi`، لهستانی `pl`، تاگالوگ `tl`، اوکراینی `uk`، هندی `hi`، یونانی `el`، کردی کرمانجی با خط لاتین `ku`، کردی سورانی با خط عربی و جهت راست‌به‌چپ `ckb` و بنگالی `bn`. راه‌اندازی، جدول و Locale آن‌ها را می‌سازد و تنظیمات قبلی را نگه می‌دارد. زبان‌های جدید تا زمان واردکردن ترجمه به فارسی برمی‌گردند. CSV جدید تمام زبان‌ها را دارد؛ CSV قبلیِ سه‌زبانه نیز قابل واردکردن است و ستون‌های حذف‌شده تغییر نمی‌کنند.

در تب **Fonts**:

1. پوشه را روی `Assets` بگذارید و **Scan all texts & fonts** را بزنید. Sceneها، Prefabها و متن‌های غیرفعال بررسی می‌شوند. نمونه‌ی Scene و دارایی Prefab جداگانه شمارش می‌شوند.
2. با **Show texts** متن، مسیر دارایی و مسیر Hierarchy هر استفاده از فونت را ببینید. فهرست دارایی‌های TMP و فایل‌های TTF/OTF هم قابل نمایش است.
3. زبان را انتخاب کنید و روبه‌روی فونت اصلی، **replacement** را مشخص کنید. برای مثال می‌توانید Lalezar عنوان‌ها و Vazir متن‌ها را در ژاپنی به دو فونت متفاوت نگاشت کنید.
4. متن‌های متصل به ترجمه از این نگاشت استفاده می‌کنند. برای فعال‌کردن تعویض فونت روی تمام موارد، از جمله متن خالی و متن بدون کلید ترجمه، **Enable font switching** همان گروه را بزنید. Sceneهای باز را ذخیره کنید. دارایی‌های ذخیره‌شده قبل از تغییر در `Library/RTLLocalizationStudioBackups` پشتیبان گرفته می‌شوند.
5. **Save font rules** را بزنید و با **Preview language in open scenes** بدون Play بررسی کنید. **Restore preview** فونت و ظاهر ذخیره‌شده را برمی‌گرداند. حذف replacement به فونت پیش‌فرض زبان یا فونت اصلی برمی‌گردد.

اولویت TMP: نگاشتِ همان فونت → فونت پیش‌فرض زبان در Overview → فونت اصلی. UI Text و TextMesh قدیمی از نگاشت Font/TTF خودشان استفاده می‌کنند. LocalizedFont و LocalizedLegacyFont متن را ترجمه نمی‌کنند؛ مسئول تعویض فونت‌اند. Prefabهای متصل که در اجرا ساخته می‌شوند نیز این رفتار را دارند. اشیایی که تماماً از کد ساخته می‌شوند به این کامپوننت نیاز دارند.

**Check glyph coverage** حروف ترجمه‌های موجود را در اطلس و fallbackهای فونت TMP بررسی می‌کند و کد حروف غایب را نشان می‌دهد. تگ‌ها و جای‌نگهدارها از بررسی کنار گذاشته می‌شوند. فونت Dynamic ممکن است در اجرا حروف را اضافه کند؛ این بررسی اطلس را تغییر نمی‌دهد. وجود حروف به‌تنهایی تضمین نمایش صحیح خط‌های پیچیده نیست؛ نتیجه را با فونت واقعی پیش‌نمایش کنید. فونت مناسب هر زبان را خودتان انتخاب کنید؛ افزودن زبان، فایل فونت یا ترجمه را دانلود نمی‌کند.

آخرین گزارش در `Library/RTLLocalizationStudio` نگه‌داری می‌شود و با **Load last scan** قابل بازکردن است. **Export usage CSV** گزارش TMP را برای بررسی خارج از Unity می‌سازد؛ گزارش مستقل legacy نیز در همین پوشه ذخیره می‌شود.

## شروع سریع

1. در اولین کامپایل، تنظیمات فارسی، انگلیسی، عربی و جدول Game ایجاد می‌شوند. اگر راه‌اندازی کامل نشده بود، در Overview دکمه Set up localization را بزنید.
2. در Bulk setup محدوده را مشخص کنید. برای شروع Loaded scenes یا Selected scene objects مناسب است.
3. Scan texts را بزنید. متن‌های خالی، عددی، ورودی و موارد مستثنا به‌صورت خودکار انتخاب نمی‌شوند.
4. کلیدها و متن‌ها را بررسی کنید و Connect selected را بزنید. اتصال گروهی ظاهر متن را تغییر نمی‌دهد. Scene باز را خودتان ذخیره کنید.
5. در Translations ترجمه وارد کنید یا CSV بگیرید. فایل UTF-8 را با Excel یا Google Sheets ویرایش کنید و دوباره CSV خروجی بگیرید.
6. Preview CSV import تغییرات را قبل از اعمال نشان می‌دهد. برای فهرست کامل از Full review report استفاده کنید.
7. جهت متن تنها وقتی FollowLanguage انتخاب شود تغییر می‌کند. برای تغییر ترتیب عناصر، کامپوننت LocalizedLayout جداگانه وجود دارد.

The **SCENE LANGUAGE** bar is available above every Studio tab. Click **Persian / FA**, **English / EN** or **Arabic / AR** to preview all connected texts in loaded scenes and Prefab Mode without Play. Inactive objects are included. Missing translations fall back to the Persian source. Click the same language again to refresh after editing translations. Scene and Game views redraw using the font, digit and alignment settings of each label.

**Restore** returns the exact authored text and styles. Closing Studio, saving assets/scenes, entering Play Mode or compiling scripts ends the preview automatically. Preview uses Unity's temporary Animation Mode snapshot and does not change the saved player language. Stop an Animation-window preview before starting a language preview. Dynamic Smart templates without their runtime arguments keep their placeholders visible. Scene preview affects localized text; sibling reordering from LocalizedLayout still runs in Play Mode.

The Studio UI uses English labels because Unity IMGUI does not reliably shape Persian text. The dedicated **Preview on selected text** window remains available for an isolated single-label preview. Its language buttons change rendering style; edit its text area to inspect another translation.

## Runtime

### نوشتن متن با همان `.text` و `SetText`

متن‌های متصل‌شده به‌صورت پیش‌فرض **Auto translate written text** دارند. در Play Mode نوشتن متن فارسی با همان دستورهای معمول، ترجمه‌ی آن را از جدول پیدا می‌کند و با تغییر زبان دوباره نمایش می‌دهد:

```csharp
title.text = "شروع بازی";
score.SetText("امتیاز: {0}", 120);
```

متن فارسی باید در جدول ثبت شده باشد و ترجمه داشته باشد. برای مثال دوم، سطر فارسی `امتیاز: {0}` و انگلیسی `Score: {0}` را ثبت کنید. قالب‌های عددی شماره‌دار پشتیبانی می‌شوند؛ جمله‌های ساخته‌شده از نام بازیکن یا متن دلخواه به قالب و اتصال صریح نیاز دارند. متن ناشناخته، ورودی کاربر و متن مبهم بدون کلید مشخص تغییر نمی‌کنند. این قابلیت ترجمه‌ی آنلاین یا حدس‌زدن معنی متن نیست.

برای متن‌های داخل کد، در **Code audit > Find Persian source phrases in code** عبارت‌ها را بررسی کنید و **Add reviewed phrases to translation table** را بزنید؛ سپس ترجمه‌ها را در Translations یا CSV وارد کنید. اسکن، عبارت‌های ثابت و برخی شرط‌های ساده و SetText را پیدا می‌کند؛ همه‌ی ترکیب‌های ممکن C# را استخراج نمی‌کند.

برای UIهایی که فقط از کد پر می‌شوند، در **Bulk setup** پس از Scan متن‌های موردنظر، از جمله موارد خالی، را دستی انتخاب کنید و **Attach auto translation for script-written text** را بزنید. اتصال گروهی انجام می‌شود و نیازی به افزودن دستی کامپوننت روی تک‌تک متن‌ها نیست. Scene یا Prefab تغییرکرده را ذخیره کنید. اشیایی که کاملاً در زمان اجرا با کد ساخته می‌شوند باید این اتصال را در Prefab یا هنگام ساخت داشته باشند.

تشخیص تغییر در LateUpdate انجام می‌شود و مقدار getter بلافاصله پس از نوشتن می‌تواند هنوز فارسی باشد. هر متن فقط با تغییر مقدار جستجو می‌شود. برای شمار زیادی متن فعال، هزینه‌ی بررسی هر فریم را در Profiler پروژه بسنجید. برای یک UI خاص می‌توانید گزینه‌ی خودکار را خاموش کنید و API کلیدی زیر را به کار ببرید.

پل RTL همراه یک تغییر سازگاری در `RTLTextMeshPro.cs` است تا SetText ارث‌بری‌شده نیز حفظ شود؛ این تغییر در `Documentation~/RTLTextMeshPro.patch` است و باید در هر پروژه‌ی دارای RTLTMPro اعمال شود.

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

See **نصب در هر پروژه** above. Unity resolves Localization and UGUI dependencies. The RTLTMPro bridge ships inside the package and stays inactive in projects without RTLTMPro. Project-specific data lives in `Assets/Localization`; do not copy game data when starting a different project. First-run setup creates a fresh workspace. This Studio targets fa/en/ar; extending its language list also requires updating the editor's CSV columns.

## Verification

**Tools > RTL Localization Studio > Validate Localization** runs integration checks and writes `Logs/localization-validation.txt`. It uses temporary objects in a preview scene and removes its temporary table keys. This checks CSV round trips, malformed input, digit conversion, workspace/fallback/preload, idempotent binding and import behavior. It does not replace visual testing of all scenes or an Android player build.

Recommended acceptance pass: switch fa → en → ar → fa; open an inactive popup; verify saved language after restart; inspect mixed names/numbers, Arabic diacritics and missing-translation fallback; check selected layout elements and a real player build.
