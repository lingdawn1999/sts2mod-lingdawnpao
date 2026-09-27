using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 倾巢（原名"倾巢而出"，2026-09-23 用户改名去掉"而出"）：1 费普通攻击，**按伏击区域张数加伤**。
    /// 造成 **7** 点伤害；你的[gold]伏击区域[/gold]中每有一张牌，伤害 +2（升级基础 7 → 9）。
    /// 写法照本体「完美打击」PerfectedStrike（2 费白攻击 6 伤 + 每张"打击"牌 +2，升级 +3，
    /// 见 sts2mod-unciphered/src/Core/Models/Cards/PerfectedStrike.cs）：三件套
    /// `CalculationBaseVar` + `ExtraDamageVar` + `CalculatedDamageVar.WithMultiplier(计数)`，
    /// 于是伤害 = 基础 + 每张 × 区域张数。
    /// **为什么这张必须做**：区域张数是本 mod 最核心的变量，但池内只有「有备而来」按张数**减费**、
    /// **没有一张按张数加伤**——埋伏流缺一个"塞满之后开火"的白卡兑现件（金卡档已有雪崩/收网）。
    /// **数值沿革**：初版基础 4（+每张 2），理由是 5 格全满 = 4+10 = 14 伤、贴着本体 1 费白卡上限（13）。
    /// **2026-09-23 用户提到基础 7**：判断"4 点打底太空、空区时几乎打不出手"，改成 7 后
    /// 空区 = 7 伤（约等于普通 1 费白卡）、满 5 格 = 7+10 = 17 伤，是**有意越过 1 费上限**的
    /// ——代价是这张牌必须先把区域铺起来，且区域会被「倾泄」「收网」「霜牙」清空，属于"先投资后兑现"的定价。
    /// 升级走**基础值**（7 → 9）而不是每张加值，避免满员时膨胀得更快。
    /// 卡面走 `{CalculatedDamage:diff()}`，会实时显示"基础 + 当前区域张数"的合计伤害。
    /// </summary>
    public sealed class SwarmAssault : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CalculationBaseVar(7m),
            new ExtraDamageVar(2m),
            new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? _) => AmbushZone.For(card.Owner).Count),
        ];

        public SwarmAssault()
            : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.CalculationBase.UpgradeValueBy(2m); // 基础 4 → 6（每张 +2 不变）
        }
    }
}