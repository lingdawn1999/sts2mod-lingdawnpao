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
    /// 雪原疾行：**1 费罕见（蓝卡）技能**（通用/运转轴），**抽 2 张牌**（升级：抽 3），**无消耗**；
    /// **[gold]埋伏[/gold]：额外抽 1 张牌**（从伏击区域**跨回合**潜伏后带出时，**不随升级成长**）。
    /// 全 mod 唯一一张"**无条件、无消耗**"的稳定过牌件——这条轴上其它卡要么带条件
    /// （藏狐/乌鸦/动物家族绑[gold]动物[/gold]、霜息/雪爪突击把抽牌挂在攻击上），
    /// 要么一次性（「暖雪」0 费抽 2，自带消耗）。
    /// <para>
    /// **2026-09-23 用户重做（原为 0 费 +1 能量 + 消耗 + 升级给固有）**：原效果整段下移到白卡「蓄能」
    /// （白卡给费），本卡腾出来补**过牌**这个洞。取"1 费换 2 张牌"的档位是因为净收益只有 +1 张，
    /// 且**每回合都要实付 1 费**——这正是它敢不设消耗、不设条件的前提。
    /// 升级走"抽 2 → 3"的质变档（同「暖雪」的升级方式）。
    /// </para>
    /// <para>
    /// **2026-09-24 用户加[gold]伏击形态[/gold]分支**：形态是玩家手动右键切换的（「伏击者」遗物），
    /// 所以那条分支等于"你正在收集埋牌的那一回合多抽 1 张"。
    /// </para>
    /// <para>
    /// **2026-09-24 第二轮（用户定案）**：把"伏击**形态**下多抽"改成"**[gold]埋伏[/gold]：额外抽 1 张**"——
    /// 判定从 `zone.IsAmbushMode`（你正在切着形态）换成 <see cref="AmbushZone.IsVeteranAmbush" />
    /// （这张牌真的在区域里跨回合潜伏过、被带出时），于是它从"看形态的通用件"变成
    /// **伏击线的回报件**：埋进去等一回合，再带出来就多抽 1 张。
    /// 数值同用户给的口径：**基础 2 张、升级只给基础 +1（→3）**，埋伏那 1 张**不升**。
    /// 本卡随之从 <see cref="ILingdawnpaoCard" /> 改为实现 <see cref="IAmbushBonusCard" />
    /// （按 `ILingdawnpaoCard` 的维护约定：带埋伏段的卡必须实现它，否则会出现
    /// "能触发埋伏但不亮金框"的不一致——现在它在伏击堆界面里会亮金框）。
    /// </para>
    /// </summary>
    public sealed class SnowfieldDash : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(2),
            new CardsVar("AmbushCards", 1),
        ];

        public SnowfieldDash()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            decimal cards = DynamicVars.Cards.BaseValue;
            if (AmbushZone.IsVeteranAmbush(this))
            {
                cards += DynamicVars["AmbushCards"].BaseValue;
            }

            await CardPileCmd.Draw(choiceContext, cards, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Cards.UpgradeValueBy(1m); // 抽 2 → 3（埋伏那 1 张刻意不升）
        }
    }
}