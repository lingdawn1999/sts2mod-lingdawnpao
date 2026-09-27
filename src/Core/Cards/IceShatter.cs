using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 冰碎瓦全：1 费普通攻击（纯数值白卡，2026-09-21 用户定稿）。
    /// 造成 15 点攻击伤害，然后**给自己挂 4 层冰冻**。升级只加伤害 15 → 21，自伤层数不变。
    /// 定位：把冰冻体系的"自伤换强度"做成一张不需要任何铺垫就能用的白卡——
    /// 前期补伤害、后期配合「融冬」（层数只增不减）或「冰封王座」做费用贷款时，
    /// 自伤那几层既是代价也是资源（层数会被算进挂冰伤害与处决线）。
    /// ⚠️ 数值沿革（四次）：
    /// ① 2026-09-21 平衡审查指出原 **18 伤 = 本体 1 费白/蓝攻击上限（13）的 1.38 倍**，
    /// 当时的处置是**加重代价**——自伤 3 → **4 层**（与「冰雪之躯」同价码），18/27 不动；
    /// ② 同日用户改判为**直接削数值**："冰碎瓦全削弱到15吧，自挂3.升级打23" →
    /// **15 伤 / 自伤 3 层 / 升级 23 伤**（伤害 -3、升级增量 9 → 8，自伤回到 3 层）。
    /// 最终 15 伤落在本体 1 费白/蓝攻击上限（13）附近，代价与数值同时收敛。
    /// ③ **2026-09-22 用户提自伤 3 → 4**：同日冰冻改成"结算后**层数减半**"
    /// （见 FrostbitePower.LayersRetainedDivisor），自伤不再逐次累积、处决线也不无限抬高，
    /// 代价被削掉大半，故三张自伤牌各 +1 补回（同「冰雪之躯」5 层、「冰封王座」每能量 4 层）。
    /// ④ **2026-09-23 用户定案升级 23 → 21**（升级增量 8 → 6）：数值体检判敲位过重，
    /// 基础 15 保留（对齐本体 1 费白/蓝攻击上限 13 档），只收敲后落点。
    /// 要再调就动 `DamageVar(15m)`、`PowerVar<FrostbitePower>(4m)` 与 `OnUpgrade` 的 `UpgradeValueBy(6m)`。
    /// ⚠️ 自伤有致死风险：挂冰瞬间若生命值低于**当时**的冰冻层数会直接触发处决（灾厄式判定），
    /// 与本 mod 其它自伤（冰封王座）同口径，属设计内的代价。
    /// </summary>
    public sealed class IceShatter : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(15m, ValueProp.Move),
            new PowerVar<FrostbitePower>(4m),
        ];

        public IceShatter()
            : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            // 自伤挂冰：applier 传空（照「冰封王座」往玩家身上挂冰时的约定），
            // 免得吃掉自己的「凝寒」加成，也免得「雪隐」因为自伤反过来给自己送格挡。
            await FrostbitePower.ApplyFrostAsync(choiceContext, Owner.Creature,
                DynamicVars["FrostbitePower"].BaseValue, null, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(6m);   // 15 → 21
        }
    }
}