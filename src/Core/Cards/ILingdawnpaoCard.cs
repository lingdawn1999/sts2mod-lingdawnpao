namespace lingdawnpao.Core
{
    /// <summary>
    /// 芝士雪豹自有卡的标记接口。用于 LingdawnpaoOwnCards.IsOwn 判定
    /// （费用雪花徽章 / 浅蓝卡框只作用于自有卡，外来角色卡保持原版配色）。
    /// 所有 mod 自建卡类都必须实现本接口。
    /// </summary>
    public interface ILingdawnpaoCard
    {
    }

    /// <summary>
    /// 「**带埋伏词条的卡**」标记接口（2026-09-23 新增）。
    /// 只有这类卡在**伏击区域里潜伏满一回合**之后，才会在伏击堆界面里亮**金框**
    /// （实现见 `NCardGrid_AssignCardsToRow_AmbushReadyGlowPatch`）——用来替代原来的"未就绪降暗"：
    /// 未就绪不再做任何标记，就绪即亮金框。
    /// <para>
    /// ⚠️ **维护约定：以后每新增一张带埋伏段的卡，都要把它的类声明从 <see cref="ILingdawnpaoCard" />
    /// 换成 <see cref="IAmbushBonusCard" />**（本接口继承自 ILingdawnpaoCard，所以雪花徽章/浅蓝卡框照旧生效）。
    /// 判定埋伏用 <see cref="AmbushZone.IsVeteranAmbush" />、亮框用
    /// <see cref="AmbushZone.IsReadyForAmbush" />，三者必须同时覆盖这张卡，否则会出现
    /// "能触发埋伏但不亮框"（或反过来）的不一致。
    /// </para>
    /// <para>当前成员（11 张）：偷袭、巧思、连环爪、扑杀、锁喉、屏息、磨爪、捕猎、暴风雪、霜牙、断腕。</para>
    /// </summary>
    public interface IAmbushBonusCard : ILingdawnpaoCard
    {
    }
}
