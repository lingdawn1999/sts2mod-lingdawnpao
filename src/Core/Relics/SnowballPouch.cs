using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core.Relics
{
    /// <summary>
    /// 专属遗物「雪球袋」（金·稀有）：你的[gold]雪球[/gold]与[gold]大雪球[/gold]造成的[gold]冰冻[/gold]层数+2。
    /// **2026-09-27 用户重做**（原"每回合开始将 1 张雪球加入手牌"被毙："这不是玩爆卡牌了"）——
    /// 从供弹引擎改为**雪球线增伤挂件**。结算收口在 <see cref="FrostbitePower.ApplyFrostAsync" />：
    /// cardSource 是雪球/大雪球时 +2，所以「雪球发射器」「打雪仗」「雪场」等**自动打出雪球**的来源同样吃加成；
    /// 自挂冰（applier=null）与其它卡牌的挂冰不吃。
    /// </summary>
    public sealed class SnowballPouch : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Rare;
    }
}
