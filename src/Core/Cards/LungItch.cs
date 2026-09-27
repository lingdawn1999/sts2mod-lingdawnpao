using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 肺痒痒：**联机专属 · 0 费蓝卡技能**（2026-09-22 用户定稿，谐音梗）。
    /// 所有玩家获得 **1** 张[gold]电子烟[/gold]。[gold]消耗[/gold]。升级：电子烟 → [gold]电子烟+[/gold]。
    /// 电子烟是无色卡（`Vape`），直接 `ModelDb.Card&lt;Vape&gt;()` 取 canonical 生成。
    /// 联机约束：`MultiplayerOnly`。
    /// </summary>
    public sealed class LungItch : CardModel, ILingdawnpaoCard
    {
        public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public LungItch()
            : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ICombatState? combatState = CombatState;
            if (combatState == null) return;

            IEnumerable<Creature> allies = from c in combatState.GetTeammatesOf(Owner.Creature)
                                           where c != null && c.IsAlive && c.IsPlayer
                                           select c;

            foreach (Creature ally in allies)
            {
                Player? allyOwner = ally.Player;
                if (allyOwner == null) continue;
                CardModel vape = ally.CombatState!.CreateCard(ModelDb.Card<Vape>(), allyOwner);
                if (IsUpgraded)
                {
                    CardCmd.Upgrade(vape); // 电子烟+
                }
                await CardPileCmd.AddGeneratedCardToCombat(vape, PileType.Hand, allyOwner);
            }
        }
    }
}
