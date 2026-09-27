using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 守住：**4 费金卡（Rare）技能**，自带[gold]消耗[/gold]。
    /// 获得 1 层[gold]无实体[/gold]；**你[gold]伏击区域[/gold]中每有一张牌，此牌的耗能就减少 1**
    /// （最低 0）。**升级：费用 4 → 3**。
    /// <para>
    /// **本体对照「灵体」`Apparition`**（1 费 · 技能 · **先古** · 自身：1 层无实体 + 虚无 + 消耗，
    /// 升级去虚无，见 sts2mod-unciphered/src/Core/Models/Cards/Apparition.cs）；
    /// 费用形状则对标本体「有备而来」的同族件——**区域越大越便宜**。
    /// **无实体**（`IntangiblePower`）＝**本回合受到的所有伤害与生命减少效果降为 1**，是全场最强防御效果，
    /// 所以定成金卡 + 高费用，靠"减费"把它从"打不起"变成"值得为它攒区域"。
    /// </para>
    /// <para>
    /// **2026-09-24 用户定稿（第三次口径）**：① **4 费起步**；② **按伏击区张数减费**（最低 0）；
    /// ③ **去掉[gold]固有[/gold]与[gold]虚无[/gold]，保留[gold]消耗[/gold]**；④ **升级只降费（4 → 3）**，
    /// 不得靠升级加无实体层数（用户裁定）。
    /// </para>
    /// <para>
    /// **为什么必须去掉固有 / 虚无**：它们与"减费"**机制上直接冲突**——
    /// 减费件的玩法是"攥在手里等区域堆起来再打"，而固有逼你开局就摸到、虚无逼你当回合用掉，
    /// 两条都等于"逼你现在就按 4 费打出去或直接消失"，减费永远攒不起来。
    /// **消耗保留**：无实体是"本回合免伤"级别的强度，能循环回来的话就是每回合免伤，与本体灵体同样带消耗。
    /// </para>
    /// <para>
    /// 折扣实现照 <see cref="ReadyForAnything" />：`EnergyCost.AddThisCombat` 做**差分重算**
    /// （`_appliedDiscount` 记住已减多少，只补差额），重算挂在 `AmbushZone.SyncCardCosts` 上，
    /// 区域每次变化（埋入/带出/清空/切换形态）都会同步一次，所以手牌里的费用显示会跟着区域实时变化。
    /// 折扣夹在基础费用 4 以内，区域扩到 5 格以上也不会算出负费用（`GetWithModifiers` 另有 0 下限兜底）。
    /// </para>
    /// </summary>
    public sealed class HoldFast : CardModel, ILingdawnpaoCard
    {
        private const int BaseCost = 4;

        private int _appliedDiscount;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<IntangiblePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<IntangiblePower>(1m),
            new EnergyVar(1),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public HoldFast()
            : base(BaseCost, CardType.Skill, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
            await PowerCmd.Apply<IntangiblePower>(choiceContext, Owner.Creature,
                DynamicVars["IntangiblePower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 4 → 3
        }

        /// <summary>按伏击区现有牌数重算该玩家所有「守住」的战斗内费用折扣（差分应用，照「有备而来」）。</summary>
        public static void SyncCosts(Player player)
        {
            if (player.PlayerCombatState == null)
            {
                return;
            }

            int target = Math.Min(AmbushZone.For(player).Count, BaseCost);
            foreach (CardModel card in player.PlayerCombatState.AllCards.ToList())
            {
                if (card is HoldFast holdFast)
                {
                    holdFast.ApplyDiscount(target);
                }
            }
        }

        private void ApplyDiscount(int target)
        {
            int delta = target - _appliedDiscount;
            if (delta == 0)
            {
                return;
            }

            AssertMutable();
            EnergyCost.AddThisCombat(-delta);
            _appliedDiscount = target;
        }
    }
}