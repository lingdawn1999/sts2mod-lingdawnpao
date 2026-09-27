using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 动物朋友：**1 费金卡能力**（2026-09-21 用户定稿）。
    /// 在你的回合开始时，随机将 **1** 张[gold]动物[/gold]加入手牌；**升级后发下来的是升级版动物**（「动物+」）。
    /// **定价锚点**：本体「无限刀刃」InfiniteBlades（1 费 · **蓝卡**能力：每回合 1 张 0 费 4 伤小刀，可叠加，
    /// 升级给固有，见 sts2mod-unciphered/src/Core/Models/Cards/InfiniteBlades.cs）——本卡是金卡，
    /// 多付的稀有度换"**动物池有 6 种、效果更杂**"（伤害/格挡/过牌/虚弱/能量/生成猛狐连击）。
    /// 可叠加（Counter）：打第二张就是每回合 2 张，与本体无限刀刃同款。
    /// **升级走"卡注入"**：能力只收得到一个 amount（那是每回合张数），所以"是否发升级版"由卡在施加后写进能力
    /// （照「雪隐」`IceArmorPower.FrostbiteStacks` 的做法，各端都会执行 OnPlay，注入结果一致）。
    /// </summary>
    public sealed class AnimalFriends : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "ANIMAL.title"), new LocString("card_keywords", "ANIMAL.description")),
            HoverTipFactory.FromPower<AnimalFriendsPower>(),
            HoverTipFactory.FromCard<DotingMom>(upgrade: IsUpgraded),
            HoverTipFactory.FromCard<PallasCat>(upgrade: IsUpgraded),
            HoverTipFactory.FromCard<TibetanFox>(upgrade: IsUpgraded),
            HoverTipFactory.FromCard<FoxTutor>(upgrade: IsUpgraded),
            HoverTipFactory.FromCard<NatureBlessing>(upgrade: IsUpgraded),
            HoverTipFactory.FromCard<SnowShelter>(upgrade: IsUpgraded),
            HoverTipFactory.FromCard<GreedyMarmot>(upgrade: IsUpgraded),
            HoverTipFactory.FromCard<DullYak>(upgrade: IsUpgraded),
            HoverTipFactory.FromCard<Bharal>(upgrade: IsUpgraded),
            HoverTipFactory.FromCard<NoisyRaven>(upgrade: IsUpgraded),
        ];

        public AnimalFriends()
            : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            AnimalFriendsPower? power = await PowerCmd.Apply<AnimalFriendsPower>(choiceContext, Owner.Creature,
                1m, Owner.Creature, this);

            if (power != null)
            {
                power.UpgradeAnimals = IsUpgraded;
            }

            Owner.GetRelic<FrostFang>()?.Flash();
        }
    }
}