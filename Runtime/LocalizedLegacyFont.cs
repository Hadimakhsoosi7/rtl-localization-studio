using UnityEngine;
using UnityEngine.UI;

namespace RTLLocalizationStudio
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RTL Localization Studio/Localized Legacy Font")]
    public sealed class LocalizedLegacyFont : MonoBehaviour
    {
        public Font authoredFont;
        public Material authoredMaterial;
        private Text ui;
        private TextMesh mesh;
        public Font SourceFont => authoredFont;
        private void OnEnable()
        {
            ui = GetComponent<Text>(); mesh = GetComponent<TextMesh>();
            if (!authoredFont) authoredFont = ui ? ui.font : mesh ? mesh.font : null;
            if (mesh && !authoredMaterial) authoredMaterial = GetComponent<Renderer>()?.sharedMaterial;
            if (!Application.isPlaying) return;
            Localization.LanguageChanged += Refresh; Refresh();
        }
        private void OnDisable() => Localization.LanguageChanged -= Refresh;
        private void Refresh() => ApplyPreview(Localization.Language);
        public void ApplyPreview(string code)
        {
            if (!ui) ui = GetComponent<Text>(); if (!mesh) mesh = GetComponent<TextMesh>();
            var font = Localization.Profile ? Localization.Profile.ResolveLegacyFont(authoredFont, code) : authoredFont;
            if (ui) ui.font = font;
            if (mesh)
            {
                mesh.font = font;
                var renderer = GetComponent<Renderer>();
                if (renderer && font) renderer.sharedMaterial = font == authoredFont && authoredMaterial ? authoredMaterial : font.material;
            }
        }
    }
}
