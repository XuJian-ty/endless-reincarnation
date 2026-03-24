using ProjectBase;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// 简易技能树面板：消耗天赋点解锁技能，并通过拖拽主动技能到技能槽位来建立槽位索引到 actionId 的映射。
    /// </summary>
    public class SkillTreePanel : BasePanel
    {
        private SkillTreePanelRuntime _runtime;

        protected override void Awake()
        {
            _runtime = new SkillTreePanelRuntime(this);
            _runtime.Initialize();
            base.Awake();
        }

        public override void ShowMe()
        {
            _runtime ??= new SkillTreePanelRuntime(this);
            _runtime.Show();
        }

        public override void HideMe()
        {
            _runtime?.Hide();
        }

        private void OnEnable()
        {
            _runtime?.OnEnable();
        }

        private void OnDisable()
        {
            _runtime?.OnDisable();
        }

        public void HandleNodeSelected(SkillTreeNodeView nodeView)
        {
            _runtime?.HandleNodeSelected(nodeView);
        }

        public void HandleNodeBeginDrag(SkillTreeNodeView nodeView, PointerEventData eventData)
        {
            _runtime?.HandleNodeBeginDrag(nodeView, eventData);
        }

        public void HandleNodeDrag(SkillTreeNodeView nodeView, PointerEventData eventData)
        {
            _runtime?.HandleNodeDrag(nodeView, eventData);
        }

        public void HandleNodeEndDrag(SkillTreeNodeView nodeView, PointerEventData eventData)
        {
            _runtime?.HandleNodeEndDrag(nodeView, eventData);
        }

        public void HandleSlotDrop(global::Game.UI.SkillTreeSlotView slotView, PointerEventData eventData)
        {
            _runtime?.HandleSlotDrop(slotView, eventData);
        }

        public void HandleSlotPointerClick(global::Game.UI.SkillTreeSlotView slotView, PointerEventData eventData)
        {
            _runtime?.HandleSlotPointerClick(slotView, eventData);
        }

    }
}
