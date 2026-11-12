using UnityEngine;
namespace RTLLocalizationStudio
{
    [AddComponentMenu("RTL Localization Studio/Language Button")]
    public sealed class LanguageButton : MonoBehaviour
    {
        public string language = "fa";
        public void SelectLanguage() => Localization.SetLanguage(language);
    }
}
