using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 动物朋友（玩家增益，由「动物朋友」卡赋予，Counter 可叠加）：
    /// **在你的回合开始时，随机把等同层数的「动物」加入手牌**（每层 1 张，基础版；卡升级过则发升级版）。
    /// 钩子用 `AfterSideTurnStart` 而不是 Before（照「雪场」：涉及"获得资源"的用 After，避免和起手抽牌抢顺序）。
    /// **随机源**：`RunState.Rng.CombatCardSelection`（战斗内的随机选牌流，与本体 Cinder / HiddenGem 同类用途一致），
    /// 从 6 只动物里等比抽一张——**联机下同一条 RNG 流，各端结果一致**。
    /// 生成方式：`combatState.CreateCard(canonical, owner)` → `CardCmd.Upgrade`（需要时）→
    /// `AddGeneratedCardsToCombat(..., PileType.Hand, owner)`，与「雪球」「猛狐连击」同一套。
    /// </summary>
    public sealed class AnimalFriendsPower : PowerModel
    {
        /// <summary>是否发升级版动物（由卡在 OnPlay 里注入，见「动物朋友」卡注释）。</summary>
        public bool UpgradeAnimals { get; set; }

        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(base.Owner) || base.Amount <= 0m)
            {
                return;
            }

            Player? owner = base.Owner.Player;
            if (owner == null)
            {
                return;
            }

            List<CardModel> generated = [];
            for (int i = 0; i < (int)base.Amount; i++)
            {
                CardModel? canonical = owner.RunState.Rng.CombatCardSelection.NextItem(Animals);
                if (canonical == null)
                {
                    continue;
                }

                CardModel animal = combatState.CreateCard(canonical, owner);
                if (UpgradeAnimals)
                {
                    CardCmd.Upgrade(animal);
                }

                generated.Add(animal);
            }

            if (generated.Count == 0)
            {
                return;
            }

            await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, owner);
        }

        /// <summary>动物池（10 只，全部为 Token 衍生牌，不进卡池；靠 ModelDb 解析）。
        /// **6 只正向件 + 4 只垃圾件**（用户 2026-09-21 定案：垃圾件用来拉低每回合期望值，
        /// 让这张金卡是"博运气"而不是"稳定收益"）：垃圾 = 贪吃的旱獭（回血）/ 迟钝的牦牛（低伤）/
        /// 路过的岩羊（低格挡）/ 吵闹的乌鸦（净换牌 0）。</summary>
        public static IEnumerable<CardModel> Animals =>
        [
            ModelDb.Card<DotingMom>(),
            ModelDb.Card<PallasCat>(),
            ModelDb.Card<TibetanFox>(),
            ModelDb.Card<FoxTutor>(),
            ModelDb.Card<NatureBlessing>(),
            ModelDb.Card<SnowShelter>(),
            ModelDb.Card<GreedyMarmot>(),
            ModelDb.Card<DullYak>(),
            ModelDb.Card<Bharal>(),
            ModelDb.Card<NoisyRaven>(),
        ];
    }
}