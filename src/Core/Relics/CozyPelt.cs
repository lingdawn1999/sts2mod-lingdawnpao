using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core.Relics
{
    /// <summary>
    /// 专属遗物「暖绒」（白·普通）：进入战斗时，获得 6 点[gold]格挡[/gold]。
    /// 2026-09-27 用户委托批量补充的专属遗物之一（收官批）——简单粗暴的开局垫甲，
    /// 与伏击体系无耦合的通用件。
    /// </summary>
    public sealed class CozyPelt : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Common;

        public override async Task BeforeCombatStart()
        {
            Player? owner = Owner;
            if (owner == null)
            {
                return;
            }

            await CreatureCmd.GainBlock(owner.Creature, 6m, ValueProp.Unpowered, null, fast: true);
        }
    }
}
