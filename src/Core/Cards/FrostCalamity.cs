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
    /// 寒灾：1 费罕见（蓝）能力。**升级：费用 1 → 0**（2026-09-23 用户改，原为"获得[gold]固有[/gold]"）。
    /// **任何带有[gold]冰冻[/gold]的敌人死亡时，其余存活敌人各获得其剩余层数的一半**（向下取整）。
    /// 结算在 <see cref="FrostCalamityPower.BeforeDeath" />（用 BeforeDeath 而非 AfterDeath：
    /// 死亡结算完目标身上的能力已被移除，取不到层数）。
    /// 定位：冰冻线的"收割结转"——单体冰冻点杀后，层数不浪费、传染给全场，
    /// 与「凝冰」（翻倍存量）、「雪崩」（兑现存量）构成"做存量 / 兑现 / 结转"三件套。
    /// 传给别人的层数照常结算挂冰伤害，可能连锁打死下一个敌人（递归安全，详见能力类注释）。
    /// **升级位沿革**：原为"给卡加[gold]固有[/gold]"（理由：遗产比例 50% 定死、加不了数值，早拿到才是强度）。
    /// **2026-09-23 用户改成"费用 1 → 0"**：① 压固有浓度（本体 13 张带固有的能力牌全是"升级给"，
    /// 但 mod 的固有总量已超本体单角色上限）；② mod 的"升级降费"浓度低于本体（7.4% vs 10.6%），
    /// 把这里的升级位换成降费正好补上。
    /// ⚠️ **副作用（观察项）**：0 费常驻回收引擎——本体 0 费能力一共只有 Panache / Neurosurge 两张，
    /// 若实测偏强，**第一个该动的是费用**（退回 1 费），不要动效果。
    /// </summary>
    public sealed class FrostCalamity : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostCalamityPower>(1m),
        ];

        public FrostCalamity()
            : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<FrostCalamityPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 1 费 → 0 费（2026-09-23 用户改，原为"获得固有"）
        }
    }
}