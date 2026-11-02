using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 冰雪之躯：**2 费**普通防御技能（纯数值白卡，2026-09-21 用户定稿）。
    /// 获得 20 点格挡，然后**给自己挂 4 层冰冻**。升级只加格挡 20 → 25，自伤层数不变。
    /// （2026-09-21 沿革：原 18 格挡 / 3 层 → 19 格挡 / 4 层；同日平衡审查指出 1 费给 19 挡
    /// 已越过本体 1 费白/蓝格挡上限（15），**用户的处置是提费用而不是降格挡**——**1 费改 2 费**。）
    /// **2026-09-22 用户提自伤 4 → 5**：同日冰冻改成"结算后**层数减半**"（见
    /// FrostbitePower.LayersRetainedDivisor），自伤不再像旧规则（-2）那样逐次累积、
    /// 处决线也不再一路爬升 → "拿命换强度"的代价被削掉大半，所以三张自伤牌的层数各 +1 补回风险。
    /// **2026-09-23 用户定稿 20/25 + 自伤 4 层**（同日改了两轮：19/28 → 18/23 → 20/25）。
    /// 本体 2 费格挡卡的档位：白卡 6 / 7 / 11 / 16（顶格「血墙」BloodWall 16），蓝卡 11~13，
    /// 金卡 Impervious 30（**带消耗**）。
    /// <para>
    /// 关键算法——**自伤会被自己刚获得的格挡吃掉**：`OnPlay` 先 `GainBlock` 再挂冰，而冰冻伤害
    /// **可被格挡**（见 FrostbitePower 里"无论目标是我方还是敌方都走格挡"的口径）→ 20 挡里被扣掉 4 点，
    /// **净 16**，正好与本体白卡顶格持平；升级 25 − 4 = 净 21。所以 20/25 是"**含代价的账面值**"，
    /// 实收落在本体白卡档内。自伤 5 → 4 同时把残留层数从 3 压到 **2**，处决阈值也跟着降低。
    /// （教训：最初只拿账面 19 去比本体白卡顶 16 就判"超模"，是漏算了自伤被格挡吃掉这一层。）
    /// </para>
    /// 定位：本 mod 防御浓度偏低，这张是"一张顶好几张防御"的重型防线，与「冰碎瓦全」成对
    /// （一攻一防，自伤价码 4 / 4 层）。
    /// ⚠️ 自伤有致死风险：挂冰瞬间若生命值低于**当时**的冰冻层数会直接触发处决（灾厄式判定），
    /// 与本 mod 其它自伤（冰封王座）同口径，属设计内的代价——血量见底时别用它硬扛。
    /// </summary>
    public sealed class IceBody : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(22m, ValueProp.Move), // 2026-10-28 用户调优：20 → 22（残留层数滚大后自伤变高，20 档后期净防劣质）
            new PowerVar<FrostbitePower>(4m),
        ];

        public IceBody()
            : base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

            // 自伤挂冰：applier 传空（照「冰封王座」往玩家身上挂冰时的约定）
            await FrostbitePower.ApplyFrostAsync(choiceContext, Owner.Creature,
                DynamicVars["FrostbitePower"].BaseValue, null, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(5m);   // 22 → 27
        }
    }
}