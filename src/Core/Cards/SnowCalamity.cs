using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪灾：**1 费蓝卡（Uncommon）技能**。**对[gold]所有敌人[/gold]**给予 14 层冰冻（升级 20）→
    /// **将 3 张「晕眩」加入伏击区域**。
    /// 定位：兑现器的**自毁型**——「重整计划」带出打出后消耗并补抽牌、「收网」把区域换成伤害，
    /// 本卡把区域换成"一次性爆发 + 三格废墟"：14 层挂冰是全 mod 最高的单卡挂冰
    /// （对照：覆霜 1 费白挂 5、豹咬 2 费白挂 7、暴风雪 2 费蓝挂 8），代价是 3 张不可打出的晕眩占格，
    /// 在清掉之前既少埋牌、也吃不到那几格的力/敏加成。
    /// **挂冰即伤害**（可被格挡），所以"挂 14 层"本身就是扎实的输出。
    /// <para>
    /// **2026-09-24 用户改回群伤（单体 → 所有敌人）**：原话"雪灾还是调整会对群吧，毕竟代价真的很大"——
    /// 亲手清空整个伏击区、再拿晕眩填满，这个自锁代价远超单体挂冰的收益，故结算面回到**全体**。
    /// 群伤走批量入口 `ApplyFrostToAllAsync`（照「霜原」），「雪隐」的格挡回调整批只触发一次。
    /// </para>
    /// <para>
    /// **2026-09-23 定稿（四项一起改）**：① 目标从全体砍回单体（原 12 层全体）；
    /// ② 数值 **12/19 → 14/22 → 14/20**（升级增量 +8 → **+6**，用户 2026-09-23 收口："上游削弱"）；
    /// ③ 填的牌从「伤口」换成「**晕眩**」；
    /// ④ **去掉自带的「消耗」词条**（本卡不再进消耗堆，可以重复打出）。
    /// **起因**：伏击区从"2 张伤口 + 1 空格"改成"真的填满"之后（遗物不再当场带走一张，见
    /// `AmbushZone.BringOutBeforeGenerateAsync` 那组改动的姊妹项），自我锁死的代价比预想中重，
    /// 用户判定必须补回来。
    /// </para>
    /// <para>
    /// ⚠️ **晕眩在区域里不会自己消散——别指望「虚无」在这里生效**：本体「虚无」只对**手牌**生效，
    /// `CombatManager.DoTurnEnd` 只遍历 `PileType.Hand.GetPile(player)`，而伏击区是 `PileType.None`。
    /// 所以晕眩躺在区域里与伤口**完全等价**（不可打出、占格、无力敏、清掉时照样吃掉一次带出额度），
    /// 换成晕眩减的负在**之后**：被带出/清出后进弃牌堆 → 之后抽到手 → 回合结束时自己消耗掉，
    /// 不会像伤口那样**永久堵手**（用户 2026-09-23 明确：不为它自研"区域消散"机制，按本体规则走）。
    /// </para>
    /// <para>
    /// ⚠️ **"反复打出 → 反复清空区域"这个循环的收益出口已经被堵掉了（2026-09-23）**：本卡每次打出都为
    /// 下一次准备燃料（自己填的晕眩），所以"反复打出"在机制上确实成立；它当初的危险是**反复触发旧版
    /// 「暗夜收割」**（伏击区内的牌被消耗 → 每张永久 +2 力量），每循环一次牌组白拿 +6~10 永久力量。
    /// 用户当天把「暗夜收割」整张重做成了"每回合开始看区域是否非空"的**回合制**成长件
    /// （见 `NightPatrolPower`），成长不再与消耗挂钩 → 循环失去收益出口。
    /// 因此本卡的"去掉消耗"现在只是"**可重复打出的单体爆发**"，没有套利空间；
    /// **也不要为了"防循环"去改那句清空效果**（比如改成不进消耗堆）——清空照旧走正常消耗流程，
    /// 否则会一并打断「重整计划」「收网」「霜牙」的既有协同。
    /// </para>
    /// <para>
    /// **群伤走批量入口**：2026-09-24 改回全体后，挂冰重新走 `ApplyFrostToAllAsync`——
    /// 逐个调单目标版会让「雪隐」按敌人数给格挡、与"每次结算只给一次"的口径冲突（照「霜原」）。
    /// **晕眩只是"占格"**：`CardType.Status` 在 `AmbushZone.RecomputeBonusesAsync` 里既不计攻击也不计技能，
    /// 因此不提供力量/敏捷（与"伏击区只给攻/技加成"的既有规则一致）。
    /// **被带出时不会崩**：晕眩自带 `CardKeyword.Unplayable`，`CardCmd.AutoPlay` 会把不可打出的牌
    /// 直接移进结果堆（弃牌堆）而不执行——所以「倾泄」/带出路径遇到晕眩只是白扔一张牌，属设计内的清偿成本。
    /// **不清空区域、固定填 3 张晕眩**（2026-09-27 用户定稿）：惩罚与上限解耦——上限硬顶 9 后
    /// "填满"的自锁随上限无界变重，对不玩伏击的构筑是过罚；固定 3 张回到原定价量级。
    /// **也不烧区域**：玩家能控好伏击区（留空格接晕眩、及时清偿占格）本身就是奖励之一；
    /// 区域已满时晕眩埋不进直接不生成。结算顺序：**先挂冰 → 再填晕眩**。
    /// **遗物对本卡整张豁免**（`FrostFang.AfterCardPlayed`）：刚填的晕眩不能被遗物的 FIFO
    /// 立刻带走（否则"3 张"变 2 张）——带出已由卡内先做（BringOutBeforeGenerateAsync），
    /// 遗物这边整张豁免。
    /// </para>
    /// </summary>
    public sealed class SnowCalamity : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
            HoverTipFactory.FromCard<Dazed>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(14m),
        ];

        public SnowCalamity()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            // 先带出旧牌再填晕眩（蓄能/雪原疾行/翻雪同款：防遗物钩子事后带走刚填的晕眩）
            await AmbushZone.BringOutBeforeGenerateAsync(choiceContext, cardPlay.Target, Owner);
            if (!AmbushZone.CanContinueCombat(Owner)) return;

            // 与"每次结算只给一次"的口径冲突。先取快照——挂冰伤害可能打死敌人。
            await FrostbitePower.ApplyFrostToAllAsync(choiceContext, CombatState!.HittableEnemies.ToList(),
                DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);

            // 固定填 3 张晕眩（2026-09-27 用户定稿）：**不清空伏击区域**——玩家控好自己伏击区
            //（留好空格接晕眩 / 处理占格）本身就是奖励之一；区域已满时埋不进，晕眩直接不生成。
            AmbushZoneState zone = AmbushZone.For(Owner);
            for (int i = 0; i < 3 && AmbushZone.CanContinueCombat(Owner) && !zone.IsCoolingDown; i++)
            {
                Dazed dazed = CombatState!.CreateCard<Dazed>(Owner);
                if (!await AmbushZone.BuryGeneratedAsync(choiceContext, Owner, dazed))
                {
                    break;
                }
            }

            await AmbushZone.RefreshZoneAsync(choiceContext, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(6m); // 冰冻 14 → 20
        }
    }
}