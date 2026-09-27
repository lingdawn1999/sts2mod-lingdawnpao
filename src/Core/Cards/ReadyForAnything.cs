using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 有备而来：3 费罕见（蓝）攻击，造成 12 点伤害（升级 17）；
    /// 你[gold]伏击区域[/gold]中每有一张牌，此牌的耗能减少 1。
    /// **沿革**：初版 4 费 14/18。2026-09-23 数值体检判"区域满时 0 费 14 伤偏强"，
    /// 用户定案砍到 **4 费 12/17**（升级 +4 → +5，保持敲后落点接近）。
    /// **2026-09-24 落定为「3 费 + 蓝卡」**（用户选方案 A，伤害不动）：卡池体检显示白卡超标本体、
    /// 蓝卡反欠，这张整张上移蓝卡来填蓝卡缺口；同时**降费是它真正需要的提升**——
    /// 白卡期的它靠"便宜"取胜，蓝卡期该靠"能早打"取胜：4 费在区域没埋满时根本进不了场，
    /// 3 费则不必等到第 4 张牌。代价是折扣上限随基础费用收到 3，
    /// 但这正好与区域基础容量（3 格）一致，比原来的"夹在 4 以内"更干净。
    /// 本体同款是 BansheesCry（9 费金卡：本场每打出过一张虚无牌耗能 -1）——它用
    /// `EnergyCost.AddThisCombat` 做战斗内费用增减；但本卡的折扣会随区域**升降**（埋入加、带出减），
    /// 所以改成**差分重算**：`_appliedDiscount` 记住已经减掉多少，只补差额。
    /// 重算挂在 AmbushZone.RecomputeBonusesAsync 末尾（区域每次变化都会走那里），因此手牌里的
    /// 费用显示会跟着区域实时变化。
    /// 折扣**夹在基础费用 3 以内**：区域扩到 4 格以上时不至于出现负费用。
    /// </summary>
    public sealed class ReadyForAnything : CardModel, ILingdawnpaoCard
    {
        private const int BaseCost = 3;

        private int _appliedDiscount;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(12m, ValueProp.Move),
            new EnergyVar(1),
        ];

        public ReadyForAnything()
            : base(BaseCost, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(5m); // 12 → 17
        }

        /// <summary>按伏击区现有牌数重算该玩家所有「有备而来」的战斗内费用折扣（差分应用）。</summary>
        public static void SyncCosts(Player player)
        {
            if (player.PlayerCombatState == null)
            {
                return;
            }

            int target = Math.Min(AmbushZone.For(player).Count, BaseCost);
            foreach (CardModel card in player.PlayerCombatState.AllCards.ToList())
            {
                if (card is ReadyForAnything ready)
                {
                    ready.ApplyDiscount(target);
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