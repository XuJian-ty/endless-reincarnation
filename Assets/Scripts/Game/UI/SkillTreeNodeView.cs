using Game.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class SkillTreeNodeView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Graphic _baseRing;
        [SerializeField] private Graphic _selectionRing;
        [SerializeField] private Graphic _lockedOverlay;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Graphic _typeBadge;

        private SkillTreePanel _owner;
        private SkillConfigEntry _entry;
        private string _nodeId = string.Empty;

        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (transform as RectTransform);
        public string NodeId => _nodeId;
        public SkillConfigEntry Entry => _entry;

        public void EnsureReferencesBound()
        {
            _rectTransform = _rectTransform != null ? _rectTransform : (transform as RectTransform);
            _baseRing = _baseRing != null ? _baseRing : transform.Find("BaseRing")?.GetComponent<Graphic>();
            _selectionRing = _selectionRing != null ? _selectionRing : transform.Find("SelectionRing")?.GetComponent<Graphic>();
            _lockedOverlay = _lockedOverlay != null ? _lockedOverlay : transform.Find("LockedOverlay")?.GetComponent<Graphic>();
            _iconImage = _iconImage != null ? _iconImage : transform.Find("IconMaskRoot/IconMaskGraphic/Icon")?.GetComponent<Image>();
            _typeBadge = _typeBadge != null ? _typeBadge : transform.Find("TypeBadgeRoot/TypeBadge")?.GetComponent<Graphic>();
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
            Graphic baseRing,
            Graphic selectionRing,
            Graphic lockedOverlay,
            Image iconImage,
            Graphic typeBadge)
        {
            _rectTransform = rectTransform;
            _baseRing = baseRing;
            _selectionRing = selectionRing;
            _lockedOverlay = lockedOverlay;
            _iconImage = iconImage;
            _typeBadge = typeBadge;
        }

        public void Bind(SkillTreePanel owner, string nodeId, SkillConfigEntry entry)
        {
            EnsureReferencesBound();
            _owner = owner;
            _nodeId = nodeId ?? string.Empty;
            _entry = entry;

            if (_iconImage != null)
            {
                _iconImage.sprite = entry != null ? entry.skillIcon : null;
                _iconImage.enabled = _iconImage.sprite != null;
                _iconImage.preserveAspect = true;
            }

            if (_typeBadge != null)
                _typeBadge.color = entry == null
                    ? new Color(0.95f, 0.74f, 0.31f, 1f)
                    : entry.IsBaseSkill
                        ? new Color(0.56f, 0.78f, 0.93f, 1f)
                        : entry.IsActiveSkill
                            ? new Color(0.33f, 0.88f, 0.79f, 1f)
                            : new Color(0.95f, 0.74f, 0.31f, 1f);
        }

        public void RefreshVisual(bool isSelected, bool isUnlocked, bool canUnlock)
        {
            if (_selectionRing != null)
                _selectionRing.enabled = isSelected;

            if (_lockedOverlay != null)
                _lockedOverlay.enabled = !isUnlocked;

            if (_baseRing != null)
            {
                _baseRing.color = isUnlocked
                    ? new Color(0.18f, 0.60f, 0.49f, 1f)
                    : canUnlock
                        ? new Color(0.69f, 0.55f, 0.21f, 1f)
                        : new Color(0.28f, 0.31f, 0.34f, 1f);
            }

            if (_iconImage != null)
                _iconImage.color = isUnlocked || canUnlock
                    ? Color.white
                    : new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
                _owner?.HandleNodeSelected(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
                _owner?.HandleNodeBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _owner?.HandleNodeDrag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _owner?.HandleNodeEndDrag(this, eventData);
        }
    }
}
