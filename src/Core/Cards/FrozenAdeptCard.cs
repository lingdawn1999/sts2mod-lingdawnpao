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
    /// 冰封：1 费罕见（蓝）**技能**（2026-09-21 由能力牌改为技能牌）。
    /// **本回合**冰冻触发造成伤害时，额外造成一次伤害（能力体 `FrozenAdeptPower`，玩家回合结束即移除）。
    /// 升级 = **费用 1 → 0**（不再是加触发次数）。
    /// **2026-09-23 用户去掉[gold]消耗[/gold]**（原配置"技能 + 消耗 + 本回合 + 升级降费"）。
    /// 去掉的理由：效果本身已经是"**本回合**"限定，再叠一层消耗是**两道同向的限缩**，手感过重。
    /// ⚠️ **已知副作用（观察项）**：去掉消耗 + 升级降费之后，升级版是 **0 费且不消耗**，
    /// 等于"每次抽到就免费多一次冰冻触发"。若实测偏强，**第一个该动的是升级位**
    /// （改回"升级 +1 次触发"，或把消耗加回来），**不要动基础效果**。
    /// 变化沿革：原为"1 费蓝能力 + 虚无，常驻 +1 次触发，升级去虚无"→ 中间改过"升级 +1 次"→
    /// 2026-09-21 定稿改成"技能 + 消耗 + 本回合 + 升级降费"，**虚无与常驻都取消**。
    /// </summary>
    public sealed class FrozenAdeptCard : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrozenAdeptPower>(),
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrozenAdeptPower>(1m),
        ];

        public FrozenAdeptCard()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
            await PowerCmd.Apply<FrozenAdeptPower>(choiceContext, Owner.Creature,
                DynamicVars["FrozenAdeptPower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 1 费 → 0 费
        }
    }
}