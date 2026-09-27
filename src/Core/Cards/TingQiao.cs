using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 听桥：2 费**稀有（金）**技能，**减伤 + 反弹**的防守姿态（2026-09-21 用户定稿）。
    /// 直到你的下个回合开始：你受到的伤害**减少 60%**，且**受到攻击时对攻击者造成"减伤前"的伤害**（可吃力量）。
    /// 自带**保留**与**消耗**；升级 = **费用 2 → 1**；**打出后立即结束你的回合**（2026-09-21 用户追加）。
    /// **2026-09-23 用户升金（蓝 → 金）**：原话"它的机制复杂度不适合作为蓝卡"——百分比减伤 + 动态反弹
    /// 两段机制 + 保留/消耗 + 强制结束回合，确实够金卡格调（对照本体 FlameBarrier 只是固定值反弹的蓝卡）。
    /// **名字出处**：用户指定，参考《洛克王国：世界》的同名技能「听桥」。
    /// **结构同本体「火焰屏障」FlameBarrier**（2 费 · 技能 · 蓝 · 自身：12 格挡 + 本回合受到攻击时
    /// 对攻击者造成 4 伤，见 sts2mod-unciphered/src/Core/Models/Cards/FlameBarrier.cs）——
    /// 同样是"技能牌挂一个受击反弹能力"，差别在于我们给的是**百分比减伤**且反弹**动态等于来袭伤害**
    /// （不是固定值），并额外带保留。
    /// **保留 + 消耗的组合**：不打出时它会一直留在手里（保留），打出去后进消耗堆（消耗）——
    /// 让玩家挑"哪一回合承伤"再兑现。
    /// **实现细节与三处坑**（减伤钩子、反弹为何要自己记原始值、为何能吃力量）全部写在
    /// `TingQiaoPower` 的类注释里。
    /// <para>
    /// **2026-09-23 卡面文案拆开**：卡面只留「结束你的回合，获得[gold]听桥[/gold]直到你的下个回合开始。」——
    /// 减伤 60% 与反弹两段机制全部**下沉到 `TingQiaoPower` 的悬停描述**（用户原话："之前真的密密麻麻三行字"）。
    /// ⚠️ 因此卡面的 `HoverTipFactory.FromPower<TingQiaoPower>()` 是玩家读细则的**唯一入口，不要删**。
    /// </para>
    /// </summary>
    public sealed class TingQiao : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<TingQiaoPower>(),
            HoverTipFactory.FromPower<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<TingQiaoPower>(1m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

        public TingQiao()
            : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
            await PowerCmd.Apply<TingQiaoPower>(choiceContext, Owner.Creature,
                DynamicVars["TingQiaoPower"].BaseValue, Owner.Creature, this);

            // **打出后立即结束回合**（用户 2026-09-21 追加）：本卡是"防守姿态"，
            // 打完就直接进入敌方回合开始承伤——能力跨过敌方回合、到**你的下个回合开始**才消失。
            // 用 `PlayerCmd.EndTurn`（内部即 `CombatManager.SetReadyToEndTurn`），它会等当前出牌动作
            // 结算完再转回合（见 CombatManager 的 WaitForActionThenEndTurn），所以在 OnPlay 里调用是安全的。
            // canBackOut: false —— 这是卡牌效果强制结束，不该让玩家再撤回。
            PlayerCmd.EndTurn(Owner, canBackOut: false);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 2 费 → 1 费
        }
    }
}