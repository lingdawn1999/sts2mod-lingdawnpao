using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 伏兵：**2 费金卡能力**，**升级：费用 2 → 1**（2026-09-25 用户定案）。
    /// 你的「埋伏」效果额外触发 1 次，可叠加。
    /// 完整对标本体 Accelerant（1 费金卡能力「中毒额外触发1次」，升级 +1、可叠加）；
    /// 与我们已有的「冰封」同型——冰封是蓝卡带虚无，伏兵是金卡不带代价，一蓝一金补齐两个机制的倍增件。
    /// 实现：触发次数集中在 AmbushZone.AmbushTriggerCount，所有带埋伏效果的卡都用它包住自己的埋伏分支，
    /// 基础效果不重复触发（用户 2026-09-20 定案："只认埋伏效果"）。
    /// <para>
    /// **2026-09-25 用户改定价：1 费 → 2 费，升级位从"额外触发 1 → 2 次"改成"降费 2 → 1"**
    /// （原话"升级不再暴力加层数"）。改后**层数固定 1 层**，升级只买费用——
    /// 这张牌的效果是"把整条埋伏线的每次埋伏收益翻倍"，1 费在本 mod 的埋伏密度下太便宜；
    /// 同时"叠两张伏兵 = 4 倍埋伏收益"这种乘法叠加也被拆掉（想再翻倍得再花 2 费）。
    /// </para>
    /// </summary>
    public sealed class AmbushEcho : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<AmbushEchoPower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<AmbushEchoPower>(1m),
        ];

        public AmbushEcho()
            : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<AmbushEchoPower>(choiceContext, Owner.Creature,
                DynamicVars["AmbushEchoPower"].BaseValue, Owner.Creature, this);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 2 费 → 1 费（层数固定 1，不再随升级加）
        }
    }
}