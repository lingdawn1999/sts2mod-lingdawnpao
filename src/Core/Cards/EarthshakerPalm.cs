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
    /// <summary>伏击区域每有一张牌，对全体敌人造成6（8）点伤害一次。</summary>
    public sealed class EarthshakerPalm : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(6m, ValueProp.Move),
        ];

        public EarthshakerPalm()
            : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            int hits = AmbushZone.For(Owner).Count;
            if (hits == 0) return;
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .WithHitCount(hits).FromCardCompat(this, cardPlay)
                .WithAttackerAnim("GroundPalm", 0.633333f)
                .TargetingAllOpponents(CombatState!)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m); // 6 → 8
        }
    }
}
