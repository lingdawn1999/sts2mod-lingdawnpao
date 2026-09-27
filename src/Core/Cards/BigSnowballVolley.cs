using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 大雪球！：2 费罕见（蓝）技能。**只把「大雪球」加入手牌**——基础 2 张、升级 3 张（都是普通版）。
    /// 定位：雪球线里"一次垫最多层数"的纯产弹牌（比「雪球保护」「打雪仗」更重的一档）。
    /// <para>
    /// **2026-09-25 用户削弱**：删掉"对目标挂 4 层[gold]冰冻[/gold]"那一段（原为 4 层 + 2 张升级版），
    /// 目标类型随之从"指定单体"改成**自身**——不再和敌人交互，省掉一次无意义的选敌；
    /// 升级也从"生成的 2 张变升级版（大雪球+）"改成"**张数 2 → 3**"，品质不变（都是普通大雪球）。
    /// 这样升级买的是数量，与「雪场」升级买质量（雪球→大雪球）形成两个不同的升级方向。
    /// </para>
    /// </summary>
    public sealed class BigSnowballVolley : CardModel, ILingdawnpaoCard
    {
        /// <summary>基础产弹张数（升级 +1）。</summary>
        private const int BigSnowballCount = 2;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<BigSnowball>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("Snowballs", BigSnowballCount),
        ];

        public BigSnowballVolley()
            : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await BigSnowball.CreateIntoHandAsync(Owner, (int)DynamicVars["Snowballs"].BaseValue, CombatState!);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["Snowballs"].UpgradeValueBy(1m); // 2 张 → 3 张（品质不变，都是普通大雪球）
        }
    }
}