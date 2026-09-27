using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 暴雪瓶：**白（Common）· 战斗内 · 指向敌人**药水（2026-09-21 用户定稿："第二个：暴雪瓶：造成10层冰冻。"）。
    /// 给予目标 **10** 层[gold]冰冻[/gold]（挂冰的瞬时伤害、-2 层、处决判定全部照常走 `FrostbitePower.ApplyFrostAsync`）。
    /// **定价锚点**：① 同档位对照本体「毒药水」PoisonPotion（**白**：6 层中毒）；
    /// ② 伤害量对照本体「火焰药水」FirePotion（**白**：**20** 点伤害）——我们冰冻单价 2 伤/层（用户口径），
    /// **10 层 = 20 伤**，正好等于火焰药水的伤害量，所以落白档。
    /// 卡面数量走 `{FrostbitePower}`（药水文案不用 `:diff()`，照本体 POISON_POTION 的写法）。
    /// 卡池侧同族对照：「覆霜」1 费白卡 5 层（升级 8）——药水一次性、不占卡位，给到 10 层。
    /// </summary>
    public sealed class BlizzardBottle : PotionModel
    {
        public override PotionRarity Rarity => PotionRarity.Common;

        public override PotionUsage Usage => PotionUsage.CombatOnly;

        public override TargetType TargetType => TargetType.AnyEnemy;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(10m),
        ];

        public override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
        {
            PotionModel.AssertValidForTargetedPotion(target);

            await FrostbitePower.ApplyFrostAsync(choiceContext, target,
                DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, null);
        }
    }
}