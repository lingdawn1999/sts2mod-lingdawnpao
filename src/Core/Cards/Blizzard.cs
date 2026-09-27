using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 暴风雪：**2 费蓝卡（Uncommon）技能**，给予**所有敌人** 6 层冰冻（升级 8）。
    /// 埋伏（**跨回合**从伏击区域带出打出）：**层数额外 +3**（升级 +4，即升级后 12 层），并照「伏兵」重复触发。
    /// 挂冰瞬间按当前层数结算一次等同层数的直伤（**对敌无视格挡**，见 <see cref="FrostbitePower" />），
    /// 然后层数减半（向上取整）：基础 6 层 = 立即 6 伤 + 留 3 层处决线；埋伏 9 层 = 立即 9 伤 + 留 5 层。
    /// <para>
    /// **2026-09-25 用户重做（本轮冰冻线重校）**：原设计是"单体 8 层，埋伏改成挂给全体"，
    /// 现改成"**基础就全体，埋伏换层数**"，数值同时下修（8 → 6）。用户原话：
    /// "暴风雪可以削弱基础到 6，让伏击 +3 到 9 点，然后升级来到 8/4＝12 的群体，也能保证雪灾的地位"。
    /// **为什么要下修**：冰冻与本体「中毒」的结构不一样——挂冰是"**即时穿甲伤害 + 处决线**"，
    /// 不是纯延迟收益，照中毒的数值档位直接套会偏强。
    /// **为什么停在 12 层**：升级后 12 层全体仍**低于**「雪灾」的 14 层全体——雪灾还要付
    /// "清空伏击区 + 用晕眩填满"的重代价，地位不能被无代价的蓝卡顶掉。
    /// 以后要调就调雪灾那一侧，**不要反向削本卡**。
    /// </para>
    /// <para>
    /// 定位：冰冻线在伏击线里的**群体解**，与「扑杀」（1 费蓝 8 伤、埋伏改全体 +2 伤）成对——
    /// 扑杀是即时爆发、暴风雪是"铺层 + 处决线"。
    /// **群伤必须走批量入口** `ApplyFrostToAllAsync`：逐敌调单目标版会让「雪隐」按敌人数给格挡
    /// （本体 Afterimage 是"按牌数、不按目标数"，用户 2026-09-21 定案"每次结算只给一次"）。
    /// **目标类型也因此改成 `AllEnemies`**（2026-09-25）：基础就是全体，再要求"先选一个敌人"
    /// 会出现"选了 A 却打了全体"的 UI 与效果不一致。
    /// </para>
    /// **2026-09-21 数值沿革**：初稿 2 费挂 7（升级 10），因与白卡「豹咬」当时同数、蓝卡溢价撑不住，
    /// 定为 8 / 升级 11；2026-09-25 重做时下修为 6 / 升级 8（升级增量 +3 → +2）。
    /// </summary>
    public sealed class Blizzard : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(6m),
            new PowerVar<FrostbitePower>("AmbushBonus", 3m),
        ];

        public Blizzard()
            : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            // 基础就对全体生效；埋伏改成**额外叠层**，并按「伏兵」重复触发（照「扑杀」的结构）。
            decimal layers = DynamicVars["FrostbitePower"].BaseValue;
            if (AmbushZone.IsVeteranAmbush(this))
            {
                layers += DynamicVars["AmbushBonus"].BaseValue;
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await FrostbitePower.ApplyFrostToAllAsync(choiceContext, CombatState!.HittableEnemies.ToList(),
                        layers, Owner.Creature, this);
                }

                return;
            }

            await FrostbitePower.ApplyFrostToAllAsync(choiceContext, CombatState!.HittableEnemies.ToList(),
                layers, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(2m);   // 基础 6 → 8
            DynamicVars["AmbushBonus"].UpgradeValueBy(1m);      // 埋伏 +3 → +4（合计 12 层）
        }
    }
}