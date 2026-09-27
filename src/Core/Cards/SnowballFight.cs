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
    /// 打雪仗：1 费**普通（白）**技能。把 **3 张「雪球」**加入手牌，**自带[gold]消耗[/gold]**（2026-09-25 用户定案）。
    /// **升级：张数 3 → 4**（生成的**始终是普通雪球**，升级不再把牌升级成「雪球+」）。
    /// 定位：冰球线最纯粹的"弹仓"牌，不附带格挡。
    /// **2026-09-24 用户定案：由蓝卡下放白卡，效果不变**——理由与「雪球保护」当初下放白卡相同：
    /// 冰球线需要**白卡档**的入口，否则"子弹"要到中期才抓得齐；本卡作为纯产弹件放在白卡，
    /// 与"格挡 + 少弹"的「雪球保护」形成白卡双入口（一个偏弹、一个偏防）。
    /// <para>
    /// **2026-09-25 用户三刀**（同日实测"无法无天"：铺 2 张雪球进区域、四张一起打出去 ≈ 21 点伤害当量/1 费）：
    /// ① **加[gold]消耗[/gold]**——1 费产 4 张 0 费子弹且能反复抽到重打 = 无限弹仓，现在一张牌只产一次弹；
    /// ② **张数 4 → 3**——消耗只掐掉循环、没降单次爆发，再砍一张；3 也正好对齐**伏击区基础上限**（3 格）；
    /// ③ **升级位改成"张数 +1"**（3 → 4），产出的雪球**不再升级成「雪球+」**——
    ///    升级收益收成单一数值项（符合"降费 / 数值二选一"的约定），不再同时拿"更多张 + 每张更强"两份。
    /// 雪球本身没动（仍是 0 费攻击牌：1 伤吃力量 + 2 层挂冰）。
    /// </para>
    /// </summary>
    public sealed class SnowballFight : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Snowball>(),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("Snowballs", 3m),
        ];

        public SnowballFight()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            // 产出**永远是普通雪球**（升级只加张数）
            await Snowball.CreateIntoHandAsync(Owner, (int)DynamicVars["Snowballs"].BaseValue, CombatState!);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["Snowballs"].UpgradeValueBy(1m); // 张数 3 → 4（雪球本身不升级）
        }
    }
}