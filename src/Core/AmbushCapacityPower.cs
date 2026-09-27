using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 伏击区上限的能力化显示：层数 = 当前伏击区上限。
    /// <para>
    /// 战斗开始由 FrostFang.BeforeCombatStart 挂上 <see cref="AmbushZone.MaxSize" /> 层（暗杀者额外 +1），
    /// 「扩张」「张网以待」通过 PowerCmd.Apply 给本能力叠层加成上限；AmbushZoneState.Capacity 直接读 Amount。
    /// </para>
    /// <para>
    /// 纯显示不携带行为——之所以做成能力而不是直接改字段：用户要求"给玩家一个可见的提示"
    /// （2026-09-23："开局就会有这个 buff，buff3层提示开局有3伏击，然后叠层是加这个数量上限，
    /// 用这种方式来提示玩家"）。图标复用伏击者（霜牙）的伏击形态贴图。
    /// </para>
    /// </summary>
    public sealed class AmbushCapacityPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;
    }
}
