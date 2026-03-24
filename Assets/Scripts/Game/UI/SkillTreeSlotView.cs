using Game.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class SkillTreeSlotView : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Text _slotIndexText;
        [SerializeField] private Text _skillNameText;
        [SerializeField] private Text _hintText;
        [SerializeField] private Image _background;
        [SerializeField] private Image _iconImage;

        private SkillTreePanel _owner;
        private int _slotIndex;

        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (transform as RectTransform);
        public int SlotIndex => _slotIndex;

        public void EnsureReferencesBound()
        {
            _rectTransform = _rectTransform != null ? _rectTransform : (transform as RectTransform);
            _slotIndexText = _slotIndexText != null ? _slotIndexText : transform.Find("Txt_SlotIndex")?.GetComponent<Text>();
            _skillNameText = _skillNameText != null ? _skillNameText : transform.Find("Txt_SkillName")?.GetComponent<Text>();
            _hintText = _hintText != null ? _hintText : transform.Find("Txt_SlotHint")?.GetComponent<Text>();
            _background = _background != null ? _background : GetComponent<Image>();
            _iconImage = _iconImage != null ? _iconImage : transform.Find("IconMaskRoot/IconMaskGraphic/Icon")?.GetComponent<Image>();
        }

        private void Reset()
        {
            EnsureReferencesBound();
        }

        private void OnValidate()
        {
            EnsureReferencesBound();
        }

        public void SetupViewReferences(
            RectTransform rectTransform,
            Text slotIndexText,
            Text skillNameText,
            Text hintText,
            Image background,
            Image iconImage)
        {
            _rectTransform = rectTransform;
            _slotIndexText = slotIndexText;
            _skillNameText = skillNameText;
            _hintText = hintText;
            _background = background;
            _iconImage = iconImage;
        }

        public void Bind(SkillTreePanel owner, int slotIndex)
        {
            EnsureReferencesBound();
            _owner = owner;
            _slotIndex = slotIndex;
            if (_slotIndexText != null)
                _slotIndexText.text = $"槽位 {slotIndex + 1}";
        }

        public void RefreshVisual(SkillConfigEntry entry, bool unlocked, bool available)
        {
            if (entry == null)
            {
                if (_background != null)
                    _background.color = new Color(0.20f, 0.22f, 0.24f, 0.96f);
                if (_skillNameText != null)
                    _skillNameText.text = "空槽位";
                if (_hintText != null)
                    _hintText.text = "拖拽主动技能到此处，右键可清空";
                if (_iconImage != null)
                    _iconImage.enabled = false;
                return;
            }

            if (_background != null)
            {
                _background.color = !unlocked
                    ? new Color(0.44f, 0.30f, 0.15f, 0.98f)
                    : (available
                        ? new Color(0.16f, 0.39f, 0.32f, 0.98f)
                        : new Color(0.30f, 0.32f, 0.38f, 0.98f));
            }

            if (_skillNameText != null)
                _skillNameText.text = string.IsNullOrWhiteSpace(entry.displayName) ? entry.skillId : entry.displayName.Trim();

            if (_hintText != null)
            {
                _hintText.text = !unlocked
                    ? "技能未解锁，暂时不可释放"
                    : (available ? "已装备主动技能" : "当前形态不可释放");
            }

            if (_iconImage != null)
            {
                _iconImage.sprite = entry.skillIcon;
                _iconImage.enabled = entry.skillIcon != null;
                _iconImage.preserveAspect = true;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            _owner?.HandleSlotDrop(this, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
                _owner?.HandleSlotPointerClick(this, eventData);
        }
    }
}
