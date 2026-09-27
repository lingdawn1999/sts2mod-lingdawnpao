using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 凝冰：1 费罕见（蓝）技能，**自带[gold]保留[/gold]、带[gold]消耗[/gold]**。
    /// 使**所有敌人**的[gold]冰冻[/gold]层数翻倍（2026-09-21 用户改：保留做基础关键词、升级改给 0 费）。
    /// 只翻层数、**不触发挂冰伤害**（照本体"翻倍中毒"类效果的做法：纯粹把存量做大），
    /// 所以它不推进处决线、也不吃「凝寒」的加层——它是"存量放大器"，配套的是「雪崩」「寒灾」这种吃层数的牌。
    /// 保留 + 消耗的组合：留住它等层数高了再翻，翻完即消耗（不给第二次）。
    /// **先对 `HittableEnemies` 取快照再遍历**，与「霜原」「雪崩」一致。
    /// </summary>
    public sealed class FrostDoubling : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

        public FrostDoubling()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            foreach (Creature enemy in CombatState!.HittableEnemies.ToList())
            {
                FrostbitePower? frost = enemy.GetPower<FrostbitePower>();
                if (frost == null || frost.Amount <= 0m)
                {
                    continue;
                }

                // 加算等于当前层数 → 翻倍
                await PowerCmd.ModifyAmount(choiceContext, frost, frost.Amount, Owner.Creature, this);
            }
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 1 费 → 0 费（保留/消耗都不变）
        }
    }
}