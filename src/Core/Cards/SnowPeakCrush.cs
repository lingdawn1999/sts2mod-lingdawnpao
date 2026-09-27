using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪峰压顶：3 费罕见（蓝）攻击，**以挂冰为主体的重击**。
    /// 造成 8 点伤害，并给予 12 层[gold]冰冻[/gold]；升级 10 伤 / 16 层。
    /// <para>
    /// **2026-09-25 用户改名 + 重构伤害占比**：原名「冰峰压顶」，改名后**内部 id 同步改为
    /// `SNOW_PEAK_CRUSH`**（类名 → id 的约定），卡面文件也一并重命名为
    /// `images/packed/card_portraits/lingdawnpao/snow_peak_crush.png`——**没有做 id 别名兼容，
    /// 所以改版前的老存档里如果牌组带着这张牌，会找不到它**（用户知情后仍选"连内部 id 一起改"）。
    /// 原话："伤害结构也需要调整，尽可能拉高冰冻在里面的占比，降低直伤占比"。
    /// 18 伤 + 7 层 → **8 伤 + 12 层**（升级 24/9 → 10/16，升级增量 +6/+2 → **+2/+4**）。
    /// </para>
    /// <para>
    /// **口径（沿用本 mod 的稳态计价值 = 直伤 + 2 × 层数）**：8 + 2×12 = **32**（升级 10 + 32 = **42**），
    /// 与本体 Bludgeon 的 32 / 42 仍然精确对齐——**总值没动，只是把价值从"可格挡的直伤"
    /// 挪进"穿甲的挂冰"**。
    /// ⚠️ 但这个挪动**不是等价的**，两条真实后果：① 挂冰那段对敌**无视格挡**（2026-09-24 定稿的双标），
    /// 所以本卡对高格挡敌人**变强**；② 直伤走 `ValueProp.Move`、**吃力量**，挪进冰冻之后**力量收益变小**
    /// ——这正是"降低直伤占比"的直接代价，别再按旧版把它当力量放大器用。
    /// 单次即时输出 = 8 + 12 = 20（原 18 + 7 = 25，**变低**）；残留处决线 = ⌈12/2⌉ = 6 层（原 4 层，**变高**）。
    /// </para>
    /// <para>
    /// ⚠️ **旧定价错在哪（别再犯）**：初版按"冰冻是延迟收益"给它算了价（22 伤 + 10 层 ≈ 42 总价值，
    /// 再拿"延迟兑现"圆过去），但本 mod 的铁律是**挂冰即伤害**：<see cref="FrostbitePower.ApplyFrostAsync" />
    /// 挂完层数**立刻**按当前层数结算一段伤害（对敌无视格挡）→ "22 + 10 层"的真实即时输出是 22 + 10 = 32，
    /// 正好等于本体 Bludgeon 的 32，还白赚 5 层残留。所以估值只能用**稳态口径**（连打收敛后每层约 2 点），
    /// 一次性即时值另算，两者不能混着用。
    /// </para>
    /// <para>
    /// **为什么要做**：本 mod 3 费以上的牌占比偏薄（全池最缺"一锤定音"型攻击），
    /// 但重做后它的身份已经从"高费重击"偏向"**高费挂冰件**"。
    /// 同族对照：雪灾（2 费蓝技能 · 14 层对全体，代价是自毁伏击区）｜
    /// 暴风雪（2 费蓝技能 · 6 层全体，升级 12）｜本卡（3 费 · 8 伤 + 12 层，代价是费用，单体）。
    /// </para>
    /// </summary>
    public sealed class SnowPeakCrush : CardModel, ILingdawnpaoCard, IFusionMaterial
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        /// <summary>合成材料轴：挂冰段作为非伤害特效接线（伤害段走 DynamicVars 通用读取）。</summary>
        public async Task PlayFusionEffectAsync(PlayerChoiceContext choiceContext, CardPlay fusionPlay)
        {
            ArgumentNullException.ThrowIfNull(fusionPlay.Target, "fusionPlay.Target");

            await FrostbitePower.ApplyFrostAsync(choiceContext, fusionPlay.Target,
                DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);
        }

        /// <summary>合成卡面附加段：挂冰层数合成时刻烘焙。</summary>
        public string FusionEffectLine
        {
            get
            {
                LocString line = new("cards", "SNOW_PEAK_CRUSH.fusionEffect");
                line.Add("FrostbitePower", DynamicVars["FrostbitePower"].BaseValue);
                return line.GetFormattedText();
            }
        }

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(8m, ValueProp.Move),
            new PowerVar<FrostbitePower>(12m),
        ];

        public SnowPeakCrush()
            : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
                .Execute(choiceContext);

            await FrostbitePower.ApplyFrostAsync(choiceContext, cardPlay.Target,
                DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m);                // 8 → 10
            DynamicVars["FrostbitePower"].UpgradeValueBy(4m);     // 12 → 16
        }
    }
}