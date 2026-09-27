using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core.Relics
{
    /// <summary>
    /// 专属遗物「草丛」（蓝·罕见）：从[gold]伏击区域[/gold]带出的攻击牌，造成的伤害+2。
    /// 2026-09-27 用户定稿（"你的伏击造成伤害+2"）。实现挂 <c>CreatureCmd.Damage</c>
    /// 核心 重载（狩猎形态补丁同款位置，见 Patches.cs 的 ThicketPatch）：
    /// 以 <see cref="AmbushZone.IsAmbushPlay" /> 判定"这张伤害来自带出的伏击牌"（与「十万埋伏」
    /// 的记账同口径：跨回合潜伏过的才算），只吃[gold]攻击[/gold]伤害（IsPoweredAttack），
    /// 多段攻击每段各+2；撕裂/挂冰等 Unpowered 伤害不吃。
    /// </summary>
    public sealed class Thicket : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Uncommon;
    }
}
