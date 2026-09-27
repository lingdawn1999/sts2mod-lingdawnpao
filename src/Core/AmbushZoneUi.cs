using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 伏击堆按钮：和抽牌/弃牌/消耗堆按钮并排挂在战斗 UI 的牌堆容器里，
    /// 点击打开 NCardPileScreen 查看埋着的牌与先后顺序（本体弃牌堆同款界面）。
    /// </summary>
    public partial class NAmbushPileButton : NButton
    {
        private MegaLabel? _count;

        public override void _Ready()
        {
            // NButton 约定：子类不调 base._Ready()，改调 ConnectSignals()
            ConnectSignals();
            _count = GetNodeOrNull<MegaLabel>("CountContainer/Count");
        }

        protected override void OnRelease()
        {
            base.OnRelease();
            AmbushZoneUi.ToggleScreen();
        }

        public void SetCount(int count)
        {
            _count?.SetTextAutoSize(count.ToString());
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            AmbushZoneUi.Forget(this);
        }
    }

    public static class AmbushZoneUi
    {
        // 借用本体弃牌堆按钮的美术：mod 不打包场景/贴图，运行时实例化本体场景搬子节点过来
        private const string BorrowedScene = "combat/discard_pile";

        private static NAmbushPileButton? _button;

        public static NAmbushPileButton? Button =>
            _button != null && GodotObject.IsInstanceValid(_button) ? _button : null;

        /// <summary>伏击堆按钮中心的屏幕坐标：埋牌飞入、带出牌飞出的锚点。按钮隐藏时坐标依然有效。</summary>
        public static Vector2? GetCenter()
        {
            NAmbushPileButton? button = Button;
            return button == null ? null : button.GlobalPosition + button.Size * 0.5f;
        }

        public static void Refresh()
        {
            NAmbushPileButton? button = EnsureButton();
            if (button == null)
            {
                return;
            }

            // 战斗 UI 只展示本地玩家自己的伏击区域
            int count = AmbushZone.Local?.Count ?? 0;
            button.SetCount(count);
            button.Visible = count > 0;
        }

        /// <summary>只确保按钮节点存在（保持隐藏），让埋牌飞入动画从第一张牌起就有锚点。</summary>
        public static void Prepare()
        {
            EnsureButton();
        }

        public static void Forget(NAmbushPileButton button)
        {
            if (_button == button)
            {
                _button = null;
            }
        }

        public static void ToggleScreen()
        {
            NCapstoneContainer? container = NCapstoneContainer.Instance;
            AmbushZoneState? zone = AmbushZone.Local;
            if (container == null || zone == null || zone.Count == 0)
            {
                return;
            }

            if (container.CurrentCapstoneScreen is NCardPileScreen screen && screen.Pile == zone.Pile)
            {
                container.Close();
                return;
            }

            NCardPileScreen.ShowScreen(zone.Pile, Array.Empty<string>());
        }

        private static NAmbushPileButton? EnsureButton()
        {
            NAmbushPileButton? existing = Button;
            if (existing != null)
            {
                return existing;
            }

            NCombatUi? ui = NCombatRoom.Instance?.Ui;
            NCombatPilesContainer? container = ui?.DiscardPile?.GetParent<NCombatPilesContainer>();
            if (container == null)
            {
                return null;
            }

            var button = new NAmbushPileButton { Name = "AmbushPile" };
            BorrowVanillaVisuals(button);
            // 消耗堆上方（右侧栏）：消耗堆占 -280..-200，弃牌堆占 -95..-15
            button.AnchorLeft = 1f;
            button.AnchorTop = 1f;
            button.AnchorRight = 1f;
            button.AnchorBottom = 1f;
            button.OffsetLeft = -90f;
            button.OffsetTop = -370f;
            button.OffsetRight = -10f;
            button.OffsetBottom = -290f;
            button.GrowHorizontal = Control.GrowDirection.Begin;
            button.GrowVertical = Control.GrowDirection.Begin;
            button.PivotOffset = new Vector2(40f, 40f);
            button.Visible = false;
            container.AddChild(button);
            _button = button;
            Log.Info("伏击区域：战斗 UI 已挂接伏击堆按钮");
            return button;
        }

        private static void BorrowVanillaVisuals(NAmbushPileButton button)
        {
            Node shell;
            try
            {
                shell = SceneHelper.Instantiate<NCombatCardPile>(BorrowedScene);
            }
            catch (Exception e)
            {
                Log.Error($"伏击区域：借用本体弃牌堆美术失败：{e.Message}");
                return;
            }

            var children = new List<Node>(shell.GetChildren());
            foreach (Node child in children)
            {
                if (child.Name == "ControllerIcon")
                {
                    // 手柄热键提示图标，本按钮不绑热键
                    continue;
                }

                shell.RemoveChild(child);
                button.AddChild(child);
            }

            shell.Free();
        }
    }
}
