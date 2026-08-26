namespace SynergyUI
{
    /// <summary>
    /// 占位界面基类 —— 三个待实现界面共用 Placeholder.uxml，仅标题不同。
    /// 后续阶段各自替换为真实实现。
    /// </summary>
    public abstract class PlaceholderScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/Placeholder";

        /// <summary>界面标题，由子类提供。</summary>
        protected abstract string Title { get; }

        public override void OnEnter()
        {
            UIBinder.SetText(Root, "lbl-title", Title);
            UIBinder.BindButton(Root, "btn-back", () => Manager.Back());
        }
    }
}
