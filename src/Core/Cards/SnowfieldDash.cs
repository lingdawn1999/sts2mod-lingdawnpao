using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪原疾行：**1 费罕见（蓝卡）技能**（通用/运转轴），**抽 2 张牌，然后选择 1 张手牌加入[gold]伏击区域[/gold]**
    /// （升级：抽 2 → 3）。过牌与主动埋卡二合一——"抽牌 + 自选埋入"让这张牌在补手牌的同时
    /// 直接给区域递弹药（区域力敏 / 按区域存量的牌都吃它喂）。
    /// <para>
    /// **2026-09-27 用户重做（原为"抽 2 + [gold]埋伏[/gold]额外抽 1"）**：埋伏段整段移除
    /// （不再实现 <see cref="IAmbushBonusCard" />，它在伏击堆里不再亮金框），换成"选 1 张手牌入区"——
    /// 从"埋进去换多抽"的间接回报，改成"抽完当场选一张最好的塞进去"的直接联动。
    /// </para>
    /// <para>
    /// **埋入路径**：照暗杀者遗物的手牌埋入（<see cref="AmbushZone.BuryAsync" />，`spendResources: false`——
    /// 效果埋入本就不花钱，走同一套手牌 holder 释放 + 飞入动画管线）。区域已满 / 冷却时埋不进，
    /// 选中的牌老实留在手里（照「设伏」既有约定：不满足即空放，不做额外保护）。
    /// 选牌走 <c>CardSelectCmd.FromHand</c>：姿态牌被
    /// <see cref="CardSelectCmd_FromHand_AmbushStanceFilterPatch" /> 的过滤自动排除，不会选到说明书。
    /// </para>
    /// </summary>
    public sealed class SnowfieldDash : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(2),
        ];

        public SnowfieldDash()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

            CardModel? picked = (await CardSelectCmd.FromHand(
                context: choiceContext,
                player: Owner,
                prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
                filter: null,
                source: this)).FirstOrDefault();

            if (picked != null)
            {
                await AmbushZone.BuryAsync(choiceContext, picked, Owner, spendResources: false);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Cards.UpgradeValueBy(1m); // 抽 2 → 3
        }
    }
}
