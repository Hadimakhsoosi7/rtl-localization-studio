using UnityEngine;
namespace RTLLocalizationStudio
{
    [AddComponentMenu("RTL Localization Studio/Localized Layout")]
    public sealed class LocalizedLayout : MonoBehaviour
    {
        [Tooltip("Explicit children to reorder. Leave empty to preserve order.")]
        public Transform[] children = new Transform[0];
        [Tooltip("Optional arrow/icon to mirror. Do not assign a whole Canvas.")]
        public RectTransform directionalIcon;
        public bool originalIsRightToLeft = true;
        private int[] indices;
        private Vector3 scale;
        private void Awake()
        {
            indices = new int[children.Length];
            for (int i = 0; i < children.Length; i++) indices[i] = children[i] ? children[i].GetSiblingIndex() : -1;
            if (directionalIcon) scale = directionalIcon.localScale;
        }
        private void OnEnable() { Localization.LanguageChanged += Refresh; Refresh(); }
        private void OnDisable() => Localization.LanguageChanged -= Refresh;
        private void Refresh()
        {
            var style = Localization.Style;
            bool rtl = style != null ? style.rightToLeft : originalIsRightToLeft; // Unknown language: keep authored order.
            bool reverse = rtl != originalIsRightToLeft;
            if (indices != null && indices.Length == children.Length)
            {
                // Move all to the end first, then insert ascending; sequential SetSiblingIndex
                // otherwise shifts positions when unrelated siblings sit between the children.
                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[reverse ? children.Length - 1 - i : i];
                    if (child && indices[i] >= 0) child.SetAsLastSibling();
                }
                var slots = new int[children.Length];
                for (int i = 0; i < slots.Length; i++) slots[i] = i;
                System.Array.Sort(slots, (a, b) => indices[a].CompareTo(indices[b]));
                foreach (int i in slots)
                {
                    var child = children[reverse ? children.Length - 1 - i : i];
                    if (child && indices[i] >= 0) child.SetSiblingIndex(indices[i]);
                }
            }
            if (directionalIcon) directionalIcon.localScale = new Vector3(scale.x * (reverse ? -1 : 1), scale.y, scale.z);
        }
    }
}
