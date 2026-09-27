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
    /// 分享猎物：**联机专属 · 1 费蓝卡技能**（2026-09-22 用户定稿）。
    /// **其他**玩家获得 **2** 张[gold]猎物+[/gold]。[gold]消耗[/gold]。升级：2 → 3 张。
    /// 猎物 = `Prey`（0 费 3 伤 + 抽 1 的 Token 攻击），"猎物+" = `CardCmd.Upgrade`（抽 1 → 抽 2）。
    /// 注意是"其他玩家"——自己不给（GetTeammatesOf 排除自己）。
    /// 联机约束：`MultiplayerOnly`。
    /// </summary>
    public sealed class SharePrey : CardModel, ILingdawnpaoCard
    {
        public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(2),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public SharePrey()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            int count = DynamicVars.Cards.IntValue;

            ICombatState? combatState = CombatState;
            if (combatState == null) return;

            IEnumerable<Creature> others = from c in combatState.GetTeammatesOf(Owner.Creature)
                                           where c != null && c.IsAlive && c.IsPlayer && c != Owner.Creature
                                           select c;

            foreach (Creature ally in others)
            {
                Player? allyOwner = ally.Player;
                if (allyOwner == null) continue;

                var generated = new List<CardModel>(count);
                for (int i = 0; i < count; i++)
                {
                    CardModel prey = ally.CombatState!.CreateCard(ModelDb.Card<Prey>(), allyOwner);
                    CardCmd.Upgrade(prey); // 猎物+
                    generated.Add(prey);
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
