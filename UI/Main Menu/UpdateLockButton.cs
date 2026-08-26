using Memori.Localization;
using Memori.Tooltip;
using UnityEngine;


namespace TJ
{
    /// <summary>
    /// A lock overlay that resolves itself on Start instead of waiting for a caller.
    /// The lock reason is a localization key so the tooltip reads correctly in every locale,
    /// and the locked state is decided by the SPELLS scripting define.
    /// </summary>
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class UpdateLockButton : MonoBehaviour
    {
        [SerializeField] private MemoriTooltipTrigger _tooltipTrigger;

        [Header("Localization")]
        [Tooltip("Localization key for the lock reason, shown as the tooltip description.")]
        [SerializeField] private string _lockedDescriptionKey;

        private const string LOCKED_TITLE_KEY = "Locked";

        private void Start()
        {
            SetLockedState(IsLockedByBuild());
        }

        /// <summary>
        /// Without the SPELLS define the spell feature is compiled out of the build,
        /// so anything this overlay covers is unreachable and stays locked.
        /// </summary>
        private static bool IsLockedByBuild()
        {
#if SPELLS
            return false;
#else
            return true;
#endif
        }

        public void SetLockedState(bool isLocked)
        {
            if (_tooltipTrigger == null)
                _tooltipTrigger = GetComponent<MemoriTooltipTrigger>();

            if (isLocked)
            {
                string lockedTitle = LocalizationManager.Instance.GetText(LOCKED_TITLE_KEY);
                string lockedReason = string.IsNullOrEmpty(_lockedDescriptionKey)
                    ? string.Empty
                    : LocalizationManager.Instance.GetText(_lockedDescriptionKey);

                _tooltipTrigger.SetUpToolTip(_title: lockedTitle, _description: lockedReason);
                _tooltipTrigger.enabled = true;
            }

            gameObject.SetActive(isLocked);
        }
    }
}
