using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 有备而来：4费罕见技能，获得16（21）点格挡。
    /// 伏击区域中每有一张牌，耗能减少1；库存变化时差分刷新，最低0费。
    /// 初始三格填满后仍需1费，升级仅提高格挡。
    /// </summary>
    public sealed class ReadyForAnything : CardModel, ILingdawnpaoCard
    {
        private const int BaseCost = 4;

        private int _appliedDiscount;

        public override bool GainsBlock => true;

        protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(16m, ValueProp.Move),
            new EnergyVar(1),
        ];

        public ReadyForAnything()
            : base(BaseCost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(5m); // 16 → 21
        }

        /// <summary>按伏击区现有牌数重算该玩家所有「有备而来」的战斗内费用折扣（差分应用）。</summary>
        public static void SyncCosts(Player player)
        {
            if (player.PlayerCombatState == null)
            {
                return;
            }

            int target = Math.Min(AmbushZone.For(player).Count, BaseCost);
            foreach (CardModel card in player.PlayerCombatState.AllCards.ToList())
            {
                if (card is ReadyForAnything ready)
                {
                    ready.ApplyDiscount(target);
                }
            }
        }

        private void ApplyDiscount(int target)
        {
            int delta = target - _appliedDiscount;
            if (delta == 0)
            {
                return;
            }

            AssertMutable();
            EnergyCost.AddThisCombat(-delta);
            _appliedDiscount = target;
        }
    }
}
