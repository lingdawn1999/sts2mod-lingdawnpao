using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 狂欢时刻：**0 费**罕见（蓝）能力。**不带任何词条**（2026-09-23 用户去掉[gold]固有[/gold]）。
    /// **「雪球」与「大雪球」造成的[gold]冰冻[/gold]层数 +1**（升级 **+2**）。
    /// 雪球线的"供弹即强度"：配合「雪球保护」「打雪仗」「雪场」「大雪球！」这些批量产弹的牌，
    /// 每颗雪球都能多挂一层冰；「雪球发射器」自动打出的那一批同样吃加成（加算写在
    /// <see cref="FrostbitePower.ApplyFrostAsync" /> 里，只认 cardSource 是雪球/大雪球）。
    /// **2026-09-23 用户重做**：原效果是"每打出雪球/大雪球就获得等层数[gold]格挡[/gold]"，改为直接放大挂冰层数
    /// ——把雪球线的收益从"防御"挪到"冰冻强度"，同时把防御名额让给别处（本 mod 防御浓度本就偏低，
    /// 但用户判断这张牌改为放大冰冻更符合冰球线的定位）。
    /// **2026-09-23 去掉固有**：本体 13 张带固有的能力牌**全部是"升级给"**、没有一张基础自带；
    /// 且这张是 0 费（随时能打、不吃入场费），固有给一个 0 费牌带来的边际价值最低，故直接去掉。
    /// </summary>
    public sealed class CarnivalMoment : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Snowball>(),
            HoverTipFactory.FromCard<BigSnowball>(),
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<CarnivalMomentPower>(1m),
        ];

        public CarnivalMoment()
            : base(0, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<CarnivalMomentPower>(choiceContext, Owner.Creature,
                DynamicVars["CarnivalMomentPower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["CarnivalMomentPower"].UpgradeValueBy(1m); // 挂冰 +1 → +2
        }
    }
}