using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 暗夜收割：3 费金卡能力（**升级：成长 1/1 → 2/2**）。**每回合开始时**，若你[gold]伏击区域[/gold]中有牌，
    /// 永久获得 1 点[gold]力量[/gold]与 1 点[gold]敏捷[/gold]。
    /// <para>
    /// **定位：伏击线的通用成长件**（用户 2026-09-23 定稿）。**故意不做输出终端**——
    /// 输出终端位已经被三张卡占满：短轴「猛虎连击」、中轴「霜牙」、长轴「磨爪」。
    /// 本卡负责的是**把整条线的天花板缓慢抬上去**，而且收益刻意选"力量+敏捷"这组通用货币：
    /// 力量喂攻击与埋伏兑现，敏捷喂格挡，冰冻流也能靠敏捷保命——**所有体系都吃得到**。
    /// </para>
    /// <para>
    /// **⚠️ 可刷性是这张卡三次重做的唯一主线（前两版全部废弃，别走回头路）**：
    /// ① 初版"埋入即累积"→ 埋入是零代价主动行为，滚雪球；
    /// ② 第二版"伏击区内的牌被消耗时逐张累积"→ 被**雪球**（`CardRarity.Token`，0 费可批量生成、
    ///    自带消耗）与**晕眩**（`CardRarity.Status`，雪灾自产）刷爆，顺手还和雪灾"自产燃料再清空"
    ///    的循环联动成每循环白拿 +6~10 永久力量；
    /// ③ **本版**：把硬闸设在"**每回合一次**"，触发只读"回合开始那一刻区域是否非空"。
    /// 一回合无论堆 1 张还是 5 张、是雪球还是真牌，都只结算一次 → 燃料彻底失去套利价值，
    /// 也不需要再引入"只算真牌 / 只算花过费用的牌"那类过滤（规则面保持干净）。
    /// </para>
    /// <para>
    /// **条件为什么是"非空"**：埋伏线的常态是"埋进 → 带出 → 再埋"，带出会让区域变空，
    /// 所以"非空"是每回合都够得着的门槛（塞一张 0 费牌即可），而"已满"会把「倾泻」类玩法判死
    /// （用户 2026-09-23 明确选前者）。
    /// </para>
    /// <para>
    /// **对标**：本体「恶魔形态」3 费金卡 = 每回合 **+3 力量（升级 +4）**——
    /// ⚠️ **这个数字以实机为准**（用户 2026-09-23 校正）：项目里那份反编译快照 `sts2mod-unciphered`
    /// 写的是 `PowerVar<StrengthPower>(2m)` + `UpgradeValueBy(1m)`（即 2/3），**与实机不符**，
    /// 以后再引用本体数值不要直接采信那棵树。
    /// 本卡基础每回合 2 点属性、升级 4 点属性，**总量与本体同档**，但**拆成力+敏**——单看任何一条线
    /// 都比"纯力量"更薄（敏捷只有在你打格挡牌时才吃得满），换来的普适性与防御，且多了"区域非空"
    /// 这个每回合都要维持的门槛。**综合判断：本卡略薄于本体恶魔形态**（用户 2026-09-23 已知此结论）。
    /// **收益从下个回合开始**（同本体 DemonForm，打出当回合不结算）。
    /// 具体结算见 <see cref="NightPatrolPower" />。
    /// </para>
    /// </summary>
    public sealed class NightPatrol : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<NightPatrolPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<NightPatrolPower>(1m),
        ];

        public NightPatrol()
            : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<NightPatrolPower>(choiceContext, Owner.Creature,
                DynamicVars["NightPatrolPower"].BaseValue, Owner.Creature, this);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            DynamicVars["NightPatrolPower"].UpgradeValueBy(1m); // 成长 1/1 → 2/2（升级买成长，不降费）
        }
    }
}