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
    /// 集群伏击：**联机专属 · 1 费金卡能力**（2026-09-22 用户定稿，2026-09-25 用户重做结算方式）。
    /// **每回合开始时，令所有队友获得等同于你伏击区域当前基础增益（攻击牌数 → 力量、技能牌数 → 敏捷）
    /// 的临时力量与敏捷，持续到该队友的回合结束。**
    /// **升级：费用 1 → 0**（2026-09-23 用户改，原为"获得[gold]固有[/gold]"）。
    /// 原设计其实没错——本体 111 张能力牌里带固有的有 13 张、**且 100% 都是"升级才给"**，
    /// 是能力牌的标准升级位。改它的唯一理由是**压固有浓度**：它是 mod 8 张固有里唯一
    /// "单人玩不到"的一张，动它对单机体验零影响；同时把 mod 的升级降费浓度往本体区间（10.6%）补一点。
    /// <para>
    /// **2026-09-25 重做（本次）**：从"**持续镜像**区域力敏给队友（同增同减）"改成"**回合开始时发临时增益**"，
    /// 起因是用户发现旧形态让雪豹成了"增益架子"——区域涨一层队友就永久多一层，
    /// 最优解变成"雪豹自己什么都不干、只顾往区域里塞牌"。
    /// 现在雪豹必须**真的行动**（埋牌进区域）才能给队友换来当轮的量，且队友只在自己回合里吃到。
    /// 具体发放见 <see cref="SwarmAmbushPower.GrantToAlliesAsync" />、收回见 <see cref="SwarmAmbushGrantPower" />。
    /// </para>
    /// <para>口径：只读**基础张数**，**不含「蓄势」的加值**（用户 2026-09-25 澄清）。
    /// 联机约束：`MultiplayerOnly`。</para>
    /// </summary>
    public sealed class SwarmAmbush : CardModel, ILingdawnpaoCard
    {
        public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<SwarmAmbushPower>(),
        ];

        public SwarmAmbush()
            : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<SwarmAmbushPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            // 挂完立即发一轮：本能力的主结算在"自己的回合开始"，而打出的当回合已经过了那个点，
            // 不立刻发一次的话这张卡要等到下个回合才看得到任何效果（打出去像是空放）。
            await SwarmAmbushPower.GrantToAlliesAsync(choiceContext, Owner);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 1 费 → 0 费（2026-09-23 用户改，原为"获得固有"）
        }
    }
}
