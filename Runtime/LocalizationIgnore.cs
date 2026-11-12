using UnityEngine;
namespace RTLLocalizationStudio
{
    [DisallowMultipleComponent, AddComponentMenu("RTL Localization Studio/Ignore Localization")]
    public sealed class LocalizationIgnore : MonoBehaviour
    {
        public bool includeChildren;
        public string reason = "User content or dynamic data";
    }
}
