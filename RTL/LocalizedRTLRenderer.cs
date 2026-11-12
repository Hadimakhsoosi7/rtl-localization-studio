using RTLLocalizationStudio;
using RTLTMPro;
using TMPro;
using UnityEngine;

namespace RTLLocalizationStudio.RTL
{
    [DisallowMultipleComponent]
    public sealed class LocalizedRTLRenderer : MonoBehaviour, ILocalizedTextRenderer, ILocalizedTextSource
    {
        public string ReadSource(TMP_Text target) => target is RTLTextMeshPro native ? native.OriginalText : target.text;
        public void Render(TMP_Text target, string text, string language, bool rtl)
        {
            // Persian fallback must still be shaped when English is selected.
            if (!rtl && !string.IsNullOrEmpty(text))
                foreach (char c in text) if (c >= '\u0600' && c <= '\u06ff') { rtl = true; language = "fa"; break; }
            if (target is RTLTextMeshPro native)
            {
                // Only Arabic disables Persian shaping; English labels may still receive Persian script text.
                native.Farsi = language != "ar";
                native.ForceFix = rtl;
                native.PreserveNumbers = true;
                native.text = text;
                native.UpdateText();
                return;
            }
            target.isRightToLeftText = rtl;
            if (!rtl) { target.text = text; return; }
            var buffer = new FastStringBuilder(RTLSupport.DefaultBufferSize);
            RTLSupport.FixRTL(text ?? "", buffer, language == "fa" || language == "ckb", true, true);
            buffer.Reverse();
            target.text = buffer.ToString();
        }
    }
}
