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
    /// 雪球保护：**1 费普通（白）**技能。获得 **5** 点[gold]格挡[/gold]，并把 **2 张「雪球」**加入手牌。
    /// **升级：格挡 5 → 7；张数不变（仍是 2 张），但那 2 张变成升级版「雪球+」**（2026-09-25 用户定案）。
    /// **2026-09-23 用户定案：由蓝卡下放白卡**，并同步削弱——格挡 6 → 5（升级 9 → 8）、
    /// 升级不再给升级版雪球（张数 2 → 3，全是普通雪球）。
    /// 起因：雪球线原本 **0 张白卡入口**，冰冻流的"子弹"要到中期才抓得齐。
    /// 对照本体：Shiv 线的白卡入口是「斗篷与匕首」BladeDance（1 费 · 白 · 3 张飞刀、**纯弹无防御**）——
    /// 本卡是"少弹 + 带格挡"的交叉档。
    /// <para>
    /// **2026-09-25 用户重定升级位**（同批「打雪仗」也在挨刀）：**格挡 5 → 7（升级增量 +3 → +2）、
    /// 雪球张数不再随升级增长**（原为 2 → 3），改成"**升级后产出的 2 张是雪球+**"。
    /// 也就是两条收益（更多张 / 每张更强）里只留一条——手上的总弹量与「打雪仗」对齐（都是 2~3 张档），
    /// 升级只体现在"质量"上。白卡档产弹梯度：本卡（5 格挡 + 2 张）＜ 蓝·「打雪仗」（3 张纯弹，自带消耗）。
    /// </para>
    /// 定位：冰球线的"起手包"——一张牌同时补防御和补弹，配合「狂欢时刻」时这些雪球还能多挂一层冰。
    /// </summary>
    public sealed class SnowballGuard : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Snowball>(upgrade: IsUpgraded),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(5m, ValueProp.Move),
            new DynamicVar("Snowballs", 2m),
        ];

        public SnowballGuard()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            // 张数不随升级变化；升级只让这 2 张变成「雪球+」（2026-09-25 用户定案）
            await Snowball.CreateIntoHandAsync(Owner, (int)DynamicVars["Snowballs"].BaseValue, CombatState!,
                upgrade: IsUpgraded);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(2m); // 格挡 5 → 7（张数不动，产出的雪球变「雪球+」）
        }
    }
}