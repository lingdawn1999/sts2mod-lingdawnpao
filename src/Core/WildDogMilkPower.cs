using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Creatures;
using System.Threading.Tasks;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 「野生狗奶」（能力显示名：**永恒**）的载体；玩法为标记，另管理本地背景音乐。
    /// <para>
    /// 行为不在这里，而在 <see cref="AmbushZone.IsReady" />：它会主动查本能力，在场时把
    /// "跨回合潜伏"这个条件短路掉，于是埋入当回合即可带出并吃到埋伏收益。
    /// 之所以做成能力而不是给区域塞一个 bool：状态栏要有个图标告诉玩家效果生效中，
    /// 且 `PowerCmd.Apply` 是入队动作、联机全端一致（见 AmbushCapacityPower 的同款理由）。
    /// </para>
    /// <para>
    /// ⚠️ **刻意不碰冷却**：本能力只免"等待"，不影响「凝神」「背水一战」「过载」造成的
    /// 区域失效（那些拦的是 BuryAsync 与带出入口，不是 IsReady），2026-09-23 用户口径。
    /// </para>
    /// </summary>
    public sealed class WildDogMilkPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        /// <summary>开关型能力：重复打出只保持存在，不叠层。</summary>
        public override PowerStackType StackType => PowerStackType.Single;

        public override Task AfterApplied(Creature? applier, CardModel? cardSource)
        {
            if (Owner.Player is { } player) WildDogMilkMusic.TryStart(player);
            return Task.CompletedTask;
        }

        public override Task AfterRemoved(Creature oldOwner)
        {
            WildDogMilkMusic.StopFor(oldOwner.Player);
            return Task.CompletedTask;
        }
    }
}
