using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 呼吸：「永恒呼吸」生成的 Token 衍生技能，只能出现在伏击区域、被带出时免费打出。
    /// 0 费：回复 1 点生命（升级版「呼吸+」回 2 点），自带消耗。
    /// 是**技能牌**，所以进伏击区域按现有规则提供[gold]敏捷[/gold]（满区 3 张 = +3 敏）。
    /// **2026-09-25 用户去掉格挡**（原话"呼吸不再提供护甲，只回血"）：原为"回 1 血 + 1 格挡"，
    /// 而本牌是**技能牌**——埋在区域里期间它已经在白送敏捷了，带出后再送格挡就是一份牌吃两份防御收益；
    /// 砍掉格挡后它只剩"纯治疗"，升级位也从格挡挪到回血上。
    /// 战斗结束仍埋着的由 AmbushZone.ReturnBuriedCardsAsync 按 Token 稀有度直接销毁。
    /// </summary>
    public sealed class Breath : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/breath.tres";

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new HealVar(1m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

        public Breath()
            : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Heal.UpgradeValueBy(1m); // 回血 1 → 2
        }
    }
}