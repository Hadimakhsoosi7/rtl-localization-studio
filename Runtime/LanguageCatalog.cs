namespace RTLLocalizationStudio
{
    public static class LanguageCatalog
    {
        public const int SchemaVersion = 3;
        public static readonly string[] Codes = { "fa", "en", "ar", "zh-Hans", "ko", "ja", "fr", "de", "es", "it", "nl", "nb", "pt", "tr", "fi", "ru", "zh-Hant", "ms", "id", "th", "vi", "pl", "tl", "uk", "hi", "el", "ku", "ckb", "bn" };
        public static readonly string[] Names = { "Persian", "English", "Arabic", "Chinese (Simplified)", "Korean", "Japanese", "French", "German", "Spanish", "Italian", "Dutch", "Norwegian (Bokmal)", "Portuguese", "Turkish", "Finnish", "Russian", "Chinese (Traditional)", "Malay", "Indonesian", "Thai", "Vietnamese", "Polish", "Tagalog", "Ukrainian", "Hindi", "Greek", "Kurdish (Kurmanji)", "Kurdish (Sorani)", "Bengali" };
        public static bool IsRightToLeft(string code) => code == "fa" || code == "ar" || code == "ckb";
        public static string Name(string code)
        {
            int index = System.Array.IndexOf(Codes, code);
            return index < 0 ? code : Names[index];
        }
    }
}
