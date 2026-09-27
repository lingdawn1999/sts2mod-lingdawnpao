using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 磨爪：1 费罕见（蓝）攻击。造成 6 点伤害（升级 8），并使**本场战斗中所有「磨爪」同名牌**伤害 +4（升级 +5）。
    /// 埋伏（从伏击区带出、且跨回合潜伏过）：**额外打出一次**——整段效果连做两遍。
    /// **成长值 2 → 4 的上调（2026-09-21）**：本体同定位的「爪击」`Claw`（**0 费 · 白** · 3 伤 · 所有同名牌 +2，
    /// 升级伤/成长各 +1，见 sts2mod-unciphered/src/Core/Models/Cards/Claw.cs）成长值与改前的磨爪**完全相同**，
    /// 而磨爪是蓝卡——蓝卡溢价只靠埋伏分支撑着，基础形态毫无超越。用户裁定"**+4（激进）**"。
    /// **2026-09-23 埋伏体系大改**：埋伏段的判定从"被带出"改为"**跨回合**潜伏过"
    /// （见 <see cref="AmbushZone.IsVeteranAmbush" />），当回合埋了马上带出只走基础的"造成伤害 + 同名 +4"。
    /// **2026-09-23 第二轮（用户同日定稿）**：① 埋伏段去掉"获得 1 点能量"；② 卡面把"伏击形态下耗能为 0"
    /// 并进「埋伏」行；③ 埋伏加成 +8 → +4；④ 升级改为两个加值都 +5（`WhetClawPower` 4 → 5、`AmbushBonus` 4 → 5）。
    /// <para>
    /// **2026-09-24 用户定案（卡面与实际对齐）：埋伏段整段换成"额外打出一次"。**
    /// 缘由是旧文案承诺的形态与实机对不上（旧埋伏段是"不造成伤害、改为把同名牌加值换成更高的 `AmbushBonus`"，
    /// 结算方式偏隐晦，玩家看不出发生了什么）。改后埋伏 = 这张牌**原样再打一遍**，
    /// 表现与文案一致：两次伤害、两次加值。同时**连带删掉**了旧的"此牌进入伏击区域时不消耗费用"
    /// （`TargetDiscount` / `SyncCosts` 那套差分降费）——它原先和埋伏收益挤在同一行文案里，
    /// 用户裁定埋伏行只留"额外打出一次"，故这半句机制一并移除（**现在埋入这张牌要正常付 1 费**）。
    /// 数值未动：伤害 6 / 升级 8，成长 `WhetClawPower` 4 / 升级 5。
    /// </para>
    /// **注意滚雪球速度**：成长是永久累积且卡组里多张同名牌同时滚，+4 时打满 5 张的伤害序列是 6/10/14/18/22
    /// （5 张合计 70），之后每张继续往上走；埋伏多打一遍会让同一段序列更快堆高——这是已知并接受的激进档。
    /// 数值顺序：**先打伤害、再加值**，所以这张牌不吃它自己刚加的那一份（否则每张都是自我永动机）。
    /// </summary>
    public sealed class WhetClaw : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(6m, ValueProp.Move),
            new PowerVar<WhetClawPower>(4m),
        ];

        public WhetClaw()
            : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // 埋伏 = 额外打出一次：整段效果连做两遍（第二遍会吃到第一遍刚加的那份加值，与"真打出两次"一致）。
            int times = AmbushZone.IsVeteranAmbush(this) ? 2 : 1;
            for (int i = 0; i < times; i++)
            {
                // 先打伤害（只吃"之前积累的"加值），再加值
                decimal accumulated = Owner.Creature.GetPower<WhetClawPower>()?.Amount ?? 0m;
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue + accumulated).FromCardCompat(this, cardPlay)
                    .Targeting(cardPlay.Target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);

                await PowerCmd.Apply<WhetClawPower>(choiceContext, Owner.Creature,
                    DynamicVars["WhetClawPower"].BaseValue, Owner.Creature, this);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m);            // 6 → 8
            DynamicVars["WhetClawPower"].UpgradeValueBy(1m);  // +4 → +5
        }
    }
}