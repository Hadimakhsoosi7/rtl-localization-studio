using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace RTLLocalizationStudio
{
    public interface ILocalizedTextRenderer { void Render(TMP_Text target, string text, string language, bool rtl); }
    public interface ILocalizedTextSource { string ReadSource(TMP_Text target); }

    [DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("RTL Localization Studio/Localized Label")]
    public sealed class LocalizedLabel : MonoBehaviour
    {
        public LocalizedString reference = new LocalizedString();
        public LabelAlignment alignment = LabelAlignment.Preserve;
        public bool useLanguageDigits;
        [Tooltip("Translate registered Persian text written with TMP .text or SetText. Unknown text is preserved.")]
        public bool autoTranslateWrittenText = true;
        [TextArea] public string sourceText;
        private TMP_Text target;
        private TMP_FontAsset originalFont;
        private Material originalMaterial;
        private float originalSize, originalMin, originalMax;
        private TextAlignmentOptions originalAlignment;
        private bool captured;
        private bool subscribed, hasObserved, scriptDriven;
        private string lastVisible, lastSource, writtenSource;
        private ILocalizedTextSource sourceReader;

        private void OnEnable()
        {
            Capture();
            if (!Application.isPlaying) return;
            SourceTextIndex.Ready += ResolveWrittenSource;
            Localization.LanguageChanged += ResolveWrittenSource;
            SourceTextIndex.Initialize();
            // After the first render, compare with the last rendered display instead of the authored
            // Persian source; otherwise a label re-enabled in another language stays unsubscribed.
            bool written = autoTranslateWrittenText &&
                (hasObserved ? HasExternalWrite() : reference.IsEmpty || ReadSource() != sourceText);
            if (written) RefreshWrittenText();
            else if (scriptDriven && autoTranslateWrittenText) ResolveWrittenSource();
            else { RememberDisplay(); Subscribe(); }
        }
        private void OnDisable()
        {
            Unsubscribe();
            SourceTextIndex.Ready -= ResolveWrittenSource;
            Localization.LanguageChanged -= ResolveWrittenSource;
        }
        private void LateUpdate()
        {
            if (autoTranslateWrittenText && Application.isPlaying) RefreshWrittenText();
        }
        private void Subscribe()
        {
            if (subscribed || reference.IsEmpty) return;
            subscribed = true;
            reference.StringChanged += Render;
        }
        private void Unsubscribe()
        {
            if (!subscribed) return;
            reference.StringChanged -= Render;
            subscribed = false;
        }
        private string ReadSource() => sourceReader == null ? target.text : sourceReader.ReadSource(target);
        private bool HasExternalWrite() => !hasObserved || target.text != lastVisible || ReadSource() != lastSource;
        private void RememberDisplay()
        {
            lastVisible = target.text;
            lastSource = ReadSource();
            hasObserved = true;
        }
        /// <summary>LateUpdate checks automatically; callers can force a check when needed.</summary>
        public void RefreshWrittenText()
        {
            Capture();
            if (!autoTranslateWrittenText || !HasExternalWrite()) return;
            // RTL setters update the raw source; inherited TMP.SetText updates the visible backing string.
            writtenSource = !hasObserved || ReadSource() != lastSource ? ReadSource() : target.text;
            scriptDriven = true;
            RememberDisplay();
            Unsubscribe();
            SourceTextIndex.Initialize();
            ResolveWrittenSource();
        }
        private void ResolveWrittenSource()
        {
            if (!scriptDriven || !autoTranslateWrittenText) return;
            if (hasObserved && HasExternalWrite()) { RefreshWrittenText(); return; }
            string preferred = reference.TableEntryReference.Key ?? SourceTextIndex.KeyForId(reference.TableEntryReference.KeyId);
            if (!SourceTextIndex.TryResolve(writtenSource, preferred, out var match)) return;
            Unsubscribe();
            reference.Arguments = match.arguments;
            reference.SetReference(Localization.Profile ? Localization.Profile.table : "Game", match.key);
            Subscribe();
        }
        private void Capture()
        {
            if (captured) return;
            target = GetComponent<TMP_Text>();
            var fontBinding = GetComponent<LocalizedFont>();
            originalFont = fontBinding ? fontBinding.SourceFont : target.font;
            originalMaterial = fontBinding && fontBinding.authoredMaterial ? fontBinding.authoredMaterial : target.fontSharedMaterial;
            originalSize = target.fontSize;
            originalMin = target.fontSizeMin;
            originalMax = target.fontSizeMax;
            originalAlignment = target.alignment;
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is ILocalizedTextSource reader) { sourceReader = reader; break; }
            captured = true;
        }
        public void SetKey(string key, params object[] arguments)
        {
            Capture();
            Unsubscribe();
            scriptDriven = false;
            RememberDisplay();
            reference.Arguments = arguments;
            reference.SetReference(Localization.Profile ? Localization.Profile.table : "Game", key);
            Subscribe();
        }
        public void SetArguments(params object[] arguments)
        {
            scriptDriven = false;
            reference.Arguments = arguments;
            reference.RefreshString();
        }
        private void Render(string value)
        {
            if (autoTranslateWrittenText && hasObserved && HasExternalWrite()) { RefreshWrittenText(); return; }
            RenderPreview(value, Localization.Language);
            RememberDisplay();
        }
        /// <summary>Recapture authored style after an editor preview has been restored.</summary>
        public void ResetPreviewBaseline() => captured = false;
        public void RenderPreview(string value, string language)
        {
            Capture();
            var style = Localization.Profile ? Localization.Profile.Find(language) : null;
            bool rtl = style != null && style.rightToLeft;
            target.font = Localization.Profile ? Localization.Profile.ResolveFont(originalFont, language) : originalFont;
            if (target.font == originalFont && originalMaterial) target.fontSharedMaterial = originalMaterial;
            float scale = style == null ? 1 : style.fontScale;
            target.fontSize = originalSize * scale;
            target.fontSizeMin = originalMin * scale;
            target.fontSizeMax = originalMax * scale;
            // Preserve vertical alignment while changing only the horizontal bits.
            int horizontal = alignment == LabelAlignment.Center ? 2 : alignment == LabelAlignment.Left ? 1 :
                alignment == LabelAlignment.Right ? 4 : rtl ? 4 : 1;
            target.alignment = alignment == LabelAlignment.Preserve ? originalAlignment :
                (TextAlignmentOptions)(((int)originalAlignment & ~255) | horizontal);
            if (useLanguageDigits && style != null) value = Localization.FormatDigits(value, style.digits);
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is ILocalizedTextRenderer renderer) { renderer.Render(target, value, language, rtl); return; }
            target.text = value;
        }
    }
}
