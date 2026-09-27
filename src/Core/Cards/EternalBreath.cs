using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 永恒呼吸：1 费罕见（蓝）技能，把「呼吸」加入伏击区域**直到填满**（按当前容量与空位逐个生成）。
    /// **自带[gold]消耗[/gold]**（2026-09-24 用户定案，见下）。
    /// 与「捕猎」构成一对：捕猎 2 费固定铺 3 张攻击牌（进区域给[gold]力量[/gold]），
    /// 永恒呼吸 1 费填技能牌（给[gold]敏捷[/gold]）。
    /// **2026-09-25 用户改升级位**：原为"生成的呼吸全部为升级版"，改成**降费 1 → 0**
    /// （起因是「呼吸」本体的格挡被砍掉，升级它已经没有多少价值；且「捕猎」的升级位已经是"质变生成升级版"，
    /// 两张同族卡各占一种升级方式更好区分）。
    /// 区域已满 / 冷却时无可埋位置，打出即空放（与「捕猎」一致，不做额外保护）。
    /// **结算顺序**：先带出区域里的**旧牌**，最后才填呼吸——刚生成的呼吸不会被当场带出打掉
    /// （2026-09-23 用户定案，详见 `AmbushZone.BringOutBeforeGenerateAsync`）。
    /// <para>
    /// **2026-09-24 用户加[gold]消耗[/gold]（防无限刷治疗）**：起因是用户指出
    /// "可能会出现故意卡住小怪来刷呼吸的回血的情况"。核算属实——「呼吸」是 0 费的
    /// **技能** Token，区域基础 3 格（扩容可到 5+），所以**每打一次本卡 ≈ 3~5 点治疗
    /// + 3~5 点敏捷（埋在区域里期间）**，而本卡进弃牌堆后还会被重新抽到，**没有次数上限**；
    /// 故意留一只小怪、靠格挡顶住就能无限把血刷满再进精英/Boss（本体对此的态度见「自我修复」——
    /// 它把回血放在**战斗结束时**结算，正是为了不给刷怪留口子）。
    /// 用户裁定**掐掉来源而不是砍疗效**：加[gold]消耗[/gold]后，一局最多铺 N 次（N = 牌组里本卡的张数），
    /// 循环不再存在；「呼吸」本身"填满 → 一张张免费兑现"的手感原样保留。
    /// 代价是它从"每轮供弹的引擎件"退化成一次性铺场工具（「捕猎」那条攻击牌填充不受影响）。
    /// </para>
    /// </summary>
    public sealed class EternalBreath : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Breath>(),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public EternalBreath()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

            AmbushZoneState zone = AmbushZone.For(Owner);
            // 结算顺序（2026-09-23 用户定案）：先带出区域里的**旧牌**，最后才填呼吸。
            // 遗物钩子对本卡整张豁免，带出必须由本卡自己发起（详见 AmbushZone.BringOutBeforeGenerateAsync）。
            await AmbushZone.BringOutBeforeGenerateAsync(choiceContext, cardPlay.Target, Owner);

            // 冷却（过载代价）时埋不进任何牌，先判掉，避免凭空造出无处安放的衍生牌
            while (!zone.IsFull && !zone.IsCoolingDown)
            {
                Breath breath = CombatState!.CreateCard<Breath>(Owner);
                if (!await AmbushZone.BuryGeneratedAsync(choiceContext, Owner, breath))
                {
                    break;
                }

                await Cmd.Wait(0.1f);
            }
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 0 费（2026-09-25 用户改，原为"生成的呼吸全部为升级版"）
        }
    }
}