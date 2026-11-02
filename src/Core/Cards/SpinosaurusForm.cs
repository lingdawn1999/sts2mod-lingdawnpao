using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 棘背龙形态：1费罕见技能，获得7（10）点格挡。
    /// 移除自身全部冰冻，并给予指定敌人等量撕裂；无冰冻时仍可获得格挡。
    /// </summary>
    public sealed class SpinosaurusForm : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(7m, ValueProp.Move),
        ];

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
            HoverTipFactory.FromPower<RendPower>(),
        ];

        public SpinosaurusForm()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

            // 先取层数（移除后 power 实例就没了），1 层换 1 层撕裂
            FrostbitePower? frost = Owner.Creature.GetPower<FrostbitePower>();
            int stacks = frost == null ? 0 : (int)frost.Amount;

            if (stacks > 0)
            {
                await PowerCmd.Remove(frost!);
                await PowerCmd.Apply<RendPower>(choiceContext, cardPlay.Target, stacks, Owner.Creature, this);
            }

            Log.Info($"棘背龙形态：移除自身 {stacks} 层冰冻 → 给予目标 {stacks} 层撕裂");
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(3m); // 7 → 10；基础和升级版均保留
        }
    }
}