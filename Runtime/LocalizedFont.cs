using TMPro;
using UnityEngine;

namespace RTLLocalizationStudio
{
    [DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("RTL Localization Studio/Localized Font")]
    public sealed class LocalizedFont : MonoBehaviour
    {
        public TMP_FontAsset authoredFont;
        public Material authoredMaterial;
        private TMP_Text target;
        public TMP_FontAsset SourceFont => authoredFont ? authoredFont : (target ? target.font : GetComponent<TMP_Text>().font);
        private void OnEnable()
        {
            target = GetComponent<TMP_Text>();
            if (!authoredFont) authoredFont = target.font;
            if (!authoredMaterial) authoredMaterial = target.fontSharedMaterial;
            if (!Application.isPlaying) return;
            Localization.LanguageChanged += Refresh;
            Refresh();
        }
        private void OnDisable() => Localization.LanguageChanged -= Refresh;
        private void Refresh() => ApplyPreview(Localization.Language);
        public void ApplyPreview(string code)
        {
            if (!target) target = GetComponent<TMP_Text>();
            if (Localization.Profile)
            {
                target.font = Localization.Profile.ResolveFont(SourceFont, code);
                if (target.font == SourceFont && authoredMaterial) target.fontSharedMaterial = authoredMaterial;
            }
        }
    }
}
