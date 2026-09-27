using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 融冬（玩家增益，由「融冬」卡赋予）：**改写冰冻的触发结算**。
    /// 由你造成的冰冻触发时**不再结算伤害，且层数不再减半**——层数只增不减。
    /// 于是冰冻从"每挂一次掉一层、换一段伤害"变成纯粹的滚雪球资源——
    /// 专门用来喂「雪崩」（按层数换伤害）、「寒灾」（按层数发遗产）、「冰壳」这类吃层数的牌，
    /// 代价是放弃了挂冰自带的即时伤害与处决线推进（层数虽然涨，但不会再打出伤害）。
    /// 实际结算在 <see cref="FrostbitePower.ApplyFrostAsync" /> 里读**施加者**身上有没有这个能力
    /// （`applier?.GetPower<WinterMeltPower>()`）。
    /// <para>
    /// ⚠️ **只覆盖"你对敌方造成的冰冻"**：给自己挂冰的那几张牌（「冰雪之躯」「冰碎瓦全」「冰封王座」）
    /// 传的 `applier` 是 **null**（刻意为之——null 同时也关掉了挂冰的加成与回调，避免自伤被自己的
    /// 「凝寒」「雪隐」放大），所以**自伤类冰冻不受本能力影响**：照样结算伤害、照样减半。
    /// 2026-09-23 用户因此要求把文案从"由你造成的冰冻"改成"你对**敌方**造成的冰冻"，
    /// 免得玩家误以为融冬能免疫自伤。**别把文案改回模糊表述。**
    /// </para>
    /// **2026-09-22 用户改**：去掉了原来"每次触发额外 +Amount 层"的成长项，改成纯"不伤害、不衰减"，
    /// 所以本能力**不再需要层数**（`StackType` 由 Counter 改成 Single），升级位改成给卡加[gold]固有[/gold]。
    /// </summary>
    public sealed class WinterMeltPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;
    }
}