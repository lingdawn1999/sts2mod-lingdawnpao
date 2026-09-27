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
    /// 凝寒：**0 费**金卡（Rare）能力（2026-09-23 用户由 1 费改为 0 费）。
    /// **你每次造成冰冻时，本次施加的层数额外 +1**。
    /// 结算在 <see cref="FrostbitePower.ApplyFrostAsync" /> 的最前面（施加前加算），
    /// 所以它同时抬高"这一次的挂冰伤害"和"残留层数"——是整条冰冻线的全局乘数级放大器：
    /// 「雪场」的雪球、 「霜天」的回合挂冰、「霜原」的群挂，全部都被 +1。
    /// 只认施加者是你（冰封王座把冰冻挂到你自己身上时不吃加成）。
    /// **升级沿革**：原为"加[gold]固有[/gold]、数值不变"（顾虑是加层数会失控）。
    /// **2026-09-24 用户定案改为纯数值：+1 → +2**——起因是跨模组体检发现 mod 蓝金卡升级增量
    /// 中位数只有 1.0（本体/蕾忍/皮卡丘均为 2.0~3.0），而**金卡升级只给一个词条是最亏的一档**；
    /// 按用户的"降费或数值二选一"规则，本卡已 0 费无从降费，只能落成数值。
    /// 注意：+2 意味着放大器直接翻倍（每次挂冰都 +2），这是金卡级别的兑现，属有意为之。
    /// </summary>
    public sealed class FrostIntensify : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostIntensifyPower>(1m),
        ];

        public FrostIntensify()
            : base(0, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<FrostIntensifyPower>(choiceContext, Owner.Creature,
                DynamicVars["FrostIntensifyPower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostIntensifyPower"].UpgradeValueBy(1m); // 层数加成 1 → 2（2026-09-24 用户定案，替代原"加固有"）
        }
    }
}