using System;
using TMPro;
using UnityEngine;

namespace RTLLocalizationStudio
{
    public enum LabelAlignment { Preserve, FollowLanguage, Center, Left, Right }
    public enum DigitStyle { Preserve, Latin, Persian, Arabic }

    [CreateAssetMenu(menuName = "RTL Localization Studio/Localization Profile")]
    public sealed class LocalizationProfile : ScriptableObject
    {
        public string table = "Game";
        public string defaultLanguage = "fa";
        public string preferenceKey = "rtl-localization-studio.language";
        [HideInInspector] public int schemaVersion;
        public LanguageStyle[] languages = {
            new LanguageStyle { code = "fa", rightToLeft = true },
            new LanguageStyle { code = "en" },
            new LanguageStyle { code = "ar", rightToLeft = true }
        };
        public LanguageStyle Find(string code) => Array.Find(languages, x => x.code == code);
        public TMP_FontAsset ResolveFont(TMP_FontAsset source, string code)
        {
            var style = Find(code);
            if (style == null) return source;
            var replacement = Array.Find(style.fontReplacements ?? Array.Empty<FontReplacement>(), x => x.source == source && x.replacement);
            return replacement != null ? replacement.replacement : style.font ? style.font : source;
        }
        public Font ResolveLegacyFont(Font source, string code)
        {
            var style = Find(code);
            var rule = Array.Find(style?.legacyFontReplacements ?? Array.Empty<LegacyFontReplacement>(), x => x.source == source && x.replacement);
            return rule != null ? rule.replacement : source;
        }
    }

    [Serializable]
    public sealed class LanguageStyle
    {
        public string code;
        public bool rightToLeft;
        public TMP_FontAsset font;
        public FontReplacement[] fontReplacements = Array.Empty<FontReplacement>();
        public LegacyFontReplacement[] legacyFontReplacements = Array.Empty<LegacyFontReplacement>();
        [Range(0.5f, 2f)] public float fontScale = 1;
        public DigitStyle digits;
    }

    [Serializable]
    public sealed class FontReplacement
    {
        public TMP_FontAsset source;
        public TMP_FontAsset replacement;
    }
    [Serializable]
    public sealed class LegacyFontReplacement
    {
        public Font source;
        public Font replacement;
    }
}
