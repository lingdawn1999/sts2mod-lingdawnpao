using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 可作为「吞噬」合成材料的卡（v2 吞噬合成轴，设计稿第三节）。
    /// <para>
    /// **为什么需要这个接口**：合体卡的结算模型是"伤害段合并 + 特效各执行一次"——
    /// 伤害可以从 <c>DynamicVars.Damage</c> 通用读出，但**多段数**（连环爪的 2 段）和
    /// **非伤害特效**（雪峰压顶的挂冰、探爪的格挡）只存在于各卡自己的 OnPlay 代码里，
    /// 引擎无法凭空拆出来，必须由卡牌自己声明。
    /// </para>
    /// <para>
    /// **默认实现已经覆盖两种最常见情况**：
    /// ① 纯伤害攻击牌（打击/猛扑型）——不实现此接口，引擎回落读 <c>DynamicVars.Damage</c>、段数 1、无特效；
    /// ② 技能牌——技能合体走"整张免费重放"（<see cref="FusionEngine" /> 的 AutoPlay 路线），
    ///    同轴数值自然相加、异轴效果自然拼接，**技能牌一律不需要实现此接口**。
    /// 所以只有"带非伤害特效的攻击牌"和"多段攻击牌"需要接线（实现本接口）。
    /// </para>
    /// <para>
    /// **词条收窄（设计稿定稿）**：材料的「埋伏」词条不继承——埋伏是标记接口
    /// <see cref="IAmbushBonusCard" /> 与各卡 OnPlay 内分支的耦合，合体卡（<see cref="FusionCard" />）
    /// 天然不实现它、重放材料时也不走埋伏分支，规则自动成立，无需在此处理。
    /// </para>
    /// </summary>
    public interface IFusionMaterial
    {
        /// <summary>
        /// 材料的**基础段数**（合成时取两材料的较大者作为合体卡段数）。
        /// 默认 1；多段攻击（连环爪）覆写。埋伏分支的加段**不算**——材料按基础形态贡献。
        /// </summary>
        int FusionSegments => 1;

        /// <summary>
        /// 材料的**每段基础伤害**（合成时两材料相加）。默认 -1 = 引擎回落读
        /// <c>DynamicVars.Damage.BaseValue</c>（绝大多数攻击牌的正确答案）。
        /// 只有无 DamageVar 但仍要贡献伤害的 exotic 卡才需要覆写。
        /// </summary>
        decimal FusionDamagePerSegment => decimal.MinusOne;

        /// <summary>
        /// 材料的**非伤害特效**（作为附加段，在合体卡的伤害段全部结算完后**各执行一次，永不随段数乘**）。
        /// 纯伤害攻击牌保持默认空实现；有特效的攻击牌把 OnPlay 里"伤害以外的部分"搬进来
        /// （数值继续读自己的 DynamicVars，材料升级形态天然被继承）。
        /// </summary>
        Task PlayFusionEffectAsync(PlayerChoiceContext choiceContext, CardPlay fusionPlay) => Task.CompletedTask;

        /// <summary>
        /// 卡面附加段文本（合成时刻烘焙进合体卡描述，含 [gold] 等 BBCode；空串 = 无附加段）。
        /// 用当前语言的材料卡面数值拼出来（合成时求值一次，产物文本不再随能力变动——
        /// 与"卡面两段封顶"的动态文本方案配套，见 <see cref="FusionCard" /> 的描述注入注释）。
        /// </summary>
        string FusionEffectLine => string.Empty;
    }
}
