using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 动物家族：**联机专属 · 0 费蓝卡技能**（2026-09-22 用户定稿）。
    /// 所有玩家获得 **2** 张[gold]动物[/gold]。[gold]消耗[/gold]。升级：2 → 3 张。
    /// 动物池复用「动物朋友」`AnimalFriendsPower.Animals`（不升级，普通动物）。
    /// 联机约束：`MultiplayerOnly` —— 单人不进卡池。
    /// </summary>
    public sealed class AnimalFamily : CardModel, ILingdawnpaoCard
    {
        public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(2),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public AnimalFamily()
            : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            int count = DynamicVars.Cards.IntValue;
            var rng = Owner.RunState.Rng.CombatCardSelection;

            ICombatState? combatState = CombatState;
            if (combatState == null) return;

            IEnumerable<Creature> allies = from c in combatState.GetTeammatesOf(Owner.Creature)
                                           where c != null && c.IsAlive && c.IsPlayer
                                           select c;

            foreach (Creature ally in allies)
            {
                Player? allyOwner = ally.Player;
                if (allyOwner == null) continue;

                var generated = new List<CardModel>(count);
                for (int i = 0; i < count; i++)
                {
                    CardModel? canonical = rng.NextItem(AnimalFriendsPower.Animals);
                    if (canonical == null) continue;
                    generated.Add(ally.CombatState!.CreateCard(canonical, allyOwner));
                }
                if (generated.Count > 0)
                {
                    await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, allyOwner);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Cards.UpgradeValueBy(1m); // 2 → 3
        }
    }
}
