using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 电子烟：**1 费无色（Colorless）蓝卡能力**（2026-09-24 用户重做）。
    /// **失去 1 点[gold]生命上限[/gold]；每回合开始时抽 1 张牌**（多张「电子烟」叠加 = 每回合抽更多）。
    /// **自带[gold]虚无[/gold]，升级去掉[gold]虚无[/gold]**（照「过载」的 `RemoveKeyword(CardKeyword.Ethereal)` 写法）。
    /// <para>
    /// **定位：无色位上的"过牌引擎"**——用永久生命上限换每回合的稳定过牌。
    /// [gold]虚无[/gold] 是这张牌的价码：抽到当回合不打就没了，所以它逼玩家立刻付代价、不许观望；
    /// 升级解除这个限制（可以攥到安全的时机再打）。
    /// </para>
    /// 沿革：原为"1 费技能：获得 4 层[gold]再生[/gold] + 失去 1 点生命上限，[gold]虚无[/gold] + [gold]消耗[/gold]，
    /// 升级再生 4 → 5"。2026-09-24 用户改成能力牌 + 每回合抽牌（代价侧只留生命上限）。
    /// 实现参考本体「炽焰」BrightestFlame（从卡牌调用 `CreatureCmd.LoseMaxHp`）。
    /// 无色卡：不实现 ILingdawnpaoCard、卡面路径钉死回雪豹目录（同观星 Stargazing）。
    /// </summary>
    public sealed class Vape : CardModel
    {
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/vape.tres";

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<VapePower>(),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

        public Vape()
            : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);

            // 层数 = 打出的「电子烟」张数（每层每回合抽 1 张）
            await PowerCmd.Apply<VapePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

            // 代价：永久失去 1 点生命上限（照本体「炽焰」BrightestFlame 的写法）
            await CreatureCmd.LoseMaxHp(choiceContext, Owner.Creature, 1m, isFromCard: true);
        }

        protected override void OnUpgrade()
        {
            RemoveKeyword(CardKeyword.Ethereal); // 升级去掉虚无
        }
    }
}