using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 绒盾：芝士雪豹的基础防御，数值复刻本体防御（1 费 5 挡，升级 +3）。
    /// 类名 = ID（FUR_GUARD），卡面走 card_atlas.sprites/lingdawnpao/ 自有路径。
    /// </summary>
    public sealed class FurGuard : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(5m, ValueProp.Move),
        ];

        public FurGuard()
            : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(3m);
        }
    }
}
