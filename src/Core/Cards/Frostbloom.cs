using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 霜华迸发：1 费罕见（蓝）技能，冰冻流的**放大器**。
    /// 给予 7 层[gold]冰冻[/gold]（升级 **11**，即升级 +4）；**仅当目标已有[gold]冰冻[/gold]时才生效**。
    /// **行为照抄本体「咕嘟冒泡」BubbleBubble**（1 费 · 技能 · 蓝 · 单体：给 9 层中毒、升级 12，
    /// "如果敌方拥有中毒，则给予 9 层中毒"，见 sts2mod-unciphered/src/Core/Models/Cards/BubbleBubble.cs）：
    /// ① **卡本身可以打出**，不满足条件时只是效果不生效（**不是** `IsPlayable` 限制）——玩家自己判断；
    /// ② `ShouldGlowGoldInternal`：场上有敌人带冰冻时卡面发光（照本体同款写法）。
    /// **为什么必须做**：本 mod 的挂冰牌全是"固定层数"，**没有一张吃"已有状态"的**——
    /// 于是铺冰的收益始终是线性的，缺一个"雪球滚起来"的加速点。本卡是冰冻线的兑现闭环：
    /// 先用雪球/覆霜铺一层小的，再用它一次性叠上去。
    /// **数值沿革**：初版 8（升级 12，理由是本体 9 层毒是延迟掉血、我们即时所以压一档）。
    /// **2026-09-22 用户削到 6（升级 10）**：那个"压一档"其实抵不住"即时"这一项——
    /// 本体 9 层毒要好几回合才掉完，我们 8 层是**挂上瞬间就打 8 点**、还留 6 层；
    /// 再叠加「冰封」多触发一段、「凝寒」额外加层，实际收益明显高于本体同型卡，故再压两档。
    /// **2026-09-23 用户回调到 9（升级 13）**：上一版"压两档"压过头了，回到与本体 BubbleBubble 同档的 9 层
    /// （我们的即时结算仍由"残留层数减半"这条衰减规则兜着，不至于像本体那样慢慢掉）。
    /// **层数仍需注意**：挂冰会被「冰封」多触发一段、「凝寒」额外加层、雪球系吃「狂欢时刻」，与所有挂冰来源口径一致。
    /// </summary>
    public sealed class Frostbloom : CardModel, ILingdawnpaoCard
    {
        protected override bool ShouldGlowGoldInternal =>
            CombatState?.HittableEnemies.Any(e => e.HasPower<FrostbitePower>()) ?? false;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(9m),
        ];

        public Frostbloom()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

            if (cardPlay.Target.HasPower<FrostbitePower>())
            {
                await FrostbitePower.ApplyFrostAsync(choiceContext, cardPlay.Target,
                    DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(4m); // 7 → 11
        }
    }
}