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
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 抚摸绒绒：**联机专属 · 1 费蓝卡技能**（2026-09-22 用户定稿）。
    /// 所有玩家获得 **4** 层[gold]覆甲[/gold]。[gold]消耗[/gold]。升级：去除[gold]消耗[/gold]。
    /// 覆甲 = 本体 `PlatingPower`（Counter 型，回合结束获等同层数格挡，-1 层）。
    /// 联机约束：`MultiplayerOnly`。
    /// </summary>
    public sealed class PetTheFluffy : CardModel, ILingdawnpaoCard
    {
        public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<PlatingPower>(4m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public PetTheFluffy()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            int amount = DynamicVars["PlatingPower"].IntValue;

            ICombatState? combatState = CombatState;
            if (combatState == null) return;

            IEnumerable<Creature> allies = from c in combatState.GetTeammatesOf(Owner.Creature)
                                           where c != null && c.IsAlive && c.IsPlayer
                                           select c;

            foreach (Creature ally in allies)
            {
                await PowerCmd.Apply<PlatingPower>(choiceContext, ally, amount, Owner.Creature, this);
            }
        }

        protected override void OnUpgrade()
        {
            RemoveKeyword(CardKeyword.Exhaust); // 升级去消耗
        }
    }
}
