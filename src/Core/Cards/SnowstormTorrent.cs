using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪暴倾泻：**X 费**罕见（蓝）攻击（2026-09-21 用户定稿，2026-09-25 改数值口径）。
    /// 消耗所有能量，造成 3 点伤害 **3X** 次（升级：4 点伤害 **3X+1** 次）。
    /// <para>
    /// **为什么必须做**：全池 `HasEnergyCostX` **零匹配**——mod 完全没有 X 费牌，这是一整个缺失维度。
    /// 写法照本体「连射」Volley（0 费 X 蓝 · 随机 · 每点能量 10 伤，升级每点 +4）与「旋风斩」Whirlwind：
    /// `HasEnergyCostX => true` + `base(0, ...)` + `ResolveEnergyXValue()` 取实际支付的费用，
    /// 再拿它当 `WithHitCount` 的段数（X 费卡的标准实现方式）。
    /// **与本体的差别**：Volley 是**随机目标**（每点 10 伤），本卡是**定向单体**——
    /// 精准选定目标的代价是每点总伤更低；这也让本卡与「乱雪纷飞」分工互补
    /// （一个是软 AOE 摊给全场，一个是可集火的定向倾泻）。
    /// **2026-11-03 用户改升级位**：从"每段 3 → 4"改为"**次数 3X → 3X+1**"——
    /// 纯加每段伤在 0 能量时无感（0 段=不打），"3X+1"让升级版 **0 能量也有 1 段 4 伤保底**，
    /// 且升级是伤害+次数双增益（4 伤 × 3X+1 次：1 费 16 / 2 费 28 / 3 费 40）。
    /// </para>
    /// </summary>
    public sealed class SnowstormTorrent : CardModel, ILingdawnpaoCard
    {
        /// <summary>每点能量打几段（固定 3，升级位不再动它）。</summary>
        private const int HitsPerEnergy = 3;

        protected override bool HasEnergyCostX => true;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(3m, ValueProp.Move),
        ];

        public SnowstormTorrent()
            : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            int hits = ResolveEnergyXValue() * HitsPerEnergy;
            if (IsUpgraded)
            {
                hits += 1; // 升级：3X → 3X+1（0 能量保底 1 段）
            }

            if (hits <= 0)
            {
                return;
            }

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(hits).FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(1m); // 每段 3 → 4（升级位的另一半：次数 3X → 3X+1 在 OnPlay 里）
        }
    }
}