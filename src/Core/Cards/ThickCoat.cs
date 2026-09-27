using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 厚绒：1 费普通技能（通用牌）。获得 **8** 点[gold]格挡[/gold]（升级 11）；
    /// **若你本回合还未打出过[gold]攻击牌[/gold]，额外获得 4 点[gold]格挡[/gold]**。
    /// 打出后**不进弃牌堆，而是随机洗回抽牌堆**。
    /// <para>
    /// **2026-09-23 用户拍板重做**：打出后随机洗回抽牌堆——牌组里这张牌永远不会在弃牌堆里过夜，
    /// 每轮循环都会再被抽到，是"防御 + 循环"的混合牌。重做的坐标是用户给的：原先的"纯格挡浓度补强"（15/22）
    /// 被判定"太像本体"，要求变成**防御 + 功能的混合牌**、且**不能条件性太强**——所以这里不是"格挡 + 触发条件"，
    /// 而是**永久改写这张牌自己的去向**：规则写死、无条件、每打一次都成立。
    /// **实现不在本类里**：本体那个"出牌去向"钩子两个分支的签名不同
    /// （public：`ModifyCardPlayResultPileTypeAndPosition` 返回 `(PileType, CardPilePosition)`；
    /// public-beta：改名 `ModifyCardPlayResultLocation`、返回 `CardLocation`），而 override 必须逐字匹配签名，
    /// 一份 DLL 没法同时匹配 → 改由 Patches.cs 的 `CardPileCmd_Add_ThickCoatRecyclePatch` 挂在
    /// **下游落堆**的 `CardPileCmd.Add` 前缀上做同样的改写（那两道闸的口径见该补丁的注释）。
    /// **顶 / 底 / 随机三选一里用户最终选随机**（2026-09-23）：顶部太强（打完必然下一轮再摸到，
    /// 等于常驻 1 费 8 甲），底面又太可预判；`Random` 保证循环但不保证准时回来。
    /// 三者在 `PileType` 侧就是 `CardPilePosition.Top / Bottom / Random` 一个字面量之差，
    /// `Random` 的实际语义是"插进抽牌堆的随机下标"（`CardPileCmd` 里
    /// `Rng.Shuffle.NextInt(pile.Cards.Count + 1)`，**不重排其余牌**），
    /// 卡面文案按本体惯例写成「洗入」（本体 CHRYSALIS 的随机洗入用的就是这个词）。
    /// 先判 `pileType == PileType.Discard` 再改写 → 带[gold]消耗[/gold]（走 Exhaust 分支）、
    /// 复制品（走 None 分支）等其它去向一律原样放行，不会被这张牌劫持。
    /// </para>
    /// <para>
    /// **2026-09-24 用户拍板加条件段**：本回合**未打出过攻击牌**时额外 4 点格挡。
    /// 定位从"无条件循环防御"改成**防御轴与攻击轴的岔路口**——想拿满 12 点就得先憋住攻击牌，
    /// 一旦开了手就只剩 8 点。判定照本体 `Finisher` / `Stomp`（"本回合打出过攻击牌吗"）：
    /// `CombatManager.Instance.History.CardPlaysFinished.Any(e =&gt; e.HappenedThisTurn(CombatState) &amp;&amp; e.CardPlay.Card.Type == CardType.Attack &amp;&amp; e.CardPlay.Card.Owner == Owner)`，
    /// 不用 `CardPlaysStarted`——只有**结算完**的出牌才算"打出过"。本卡自己是技能牌，不会被自己踩到。
    /// **升级只加基础值**（8 → 11），条件段的 4 点固定，不随升级成长。
    /// </para>
    /// 数值 8/11：8 点/1 费 = 本体 2 费格挡顶格（血墙 BloodWall 16）的每费值，
    /// 换来的是"每轮必回来"的循环收益，代价是它不会即时过牌、只按费提供格挡。
    /// </summary>
    public sealed class ThickCoat : CardModel, ILingdawnpaoCard
    {
        /// <summary>本回合尚未打出攻击牌时的额外格挡（固定值，不随升级成长）。</summary>
        private const int UnattackedBonus = 4;

        public override bool GainsBlock => true;

        protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(8m, ValueProp.Move),
            new BlockVar("BonusBlock", UnattackedBonus, ValueProp.Move),
        ];

        public ThickCoat()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        /// <summary>本回合是否已经打出过攻击牌（含被带出自动打出的攻击牌）。</summary>
        private bool PlayedAttackThisTurn =>
            CombatManager.Instance.History.CardPlaysFinished.Any(e =>
                e.HappenedThisTurn(CombatState)
                && e.CardPlay.Card.Type == CardType.Attack
                && e.CardPlay.Card.Owner == Owner);

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

            if (!PlayedAttackThisTurn)
            {
                await CreatureCmd.GainBlock(Owner.Creature, (BlockVar)DynamicVars["BonusBlock"], cardPlay);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(3m);   // 8 → 11
        }
    }
}