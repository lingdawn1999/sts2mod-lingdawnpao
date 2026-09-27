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
    /// 捕猎：2 费罕见（蓝）技能，把**固定 3 张「猎物」**加入伏击区域（升级后生成的是「猎物+」）。
    /// 与「设伏」的分工：设伏是 1 费固定 2 张的常规启动件，捕猎是 2 费一次铺 3 张的铺设件，
    /// 喂「收网」「霜牙」「有备而来」的区域存量收益。
    /// 升级是**质变型**：生成的猎物全部为升级版（1 伤 → 3 伤），张数固定 3（同「设伏」的升级方式）。
    /// **2026-09-25 用户从"填满"改成"固定 3 张"**：原实现是"一直生成到区域满"，
    /// 区域被「张网以待」「扩张」扩容后一次能铺 5 张以上，配上刚削过的「猎物」依旧过量；
    /// 改成固定张数后，扩容只买到"能存更多"而不再买"一次铺更多"。
    /// 区域已满时无可埋位置，打出即空放（与「设伏」一致，不做额外保护）。
    /// **结算顺序（v2）**：直接填猎物——v2 普通出牌不再自动带出，埋进去就老实待着。
    /// </summary>
    public sealed class Hunt : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Prey>(upgrade: IsUpgraded),
            EnergyHoverTip,
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new RepeatVar(3),
            new EnergyVar(1),
        ];

        public Hunt()
            : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

            AmbushZoneState zone = AmbushZone.For(Owner);

            // 区域已满时埋不进任何牌，先判掉，避免凭空造出无处安放的衍生牌
            for (int i = 0; i < DynamicVars.Repeat.IntValue; i++)
            {
                if (zone.IsFull)
                {
                    break;
                }

                Prey prey = CombatState!.CreateCard<Prey>(Owner);
                if (IsUpgraded)
                {
                    CardCmd.Upgrade(prey);
                }

                if (!await AmbushZone.BuryGeneratedAsync(choiceContext, Owner, prey))
                {
                    break;
                }

                await Cmd.Wait(0.1f);
            }

            // 埋伏段（2026-09-23 用户新增）：这张牌**跨回合**从伏击区域被带出打出时，额外获得 1 点能量。
            // 与「设伏」的升级方向不同：捕猎买的是"铺 3 张 + 兑现时回一笔费用"，让 2 费的开销在埋伏线里能收回来。
            if (AmbushZone.IsVeteranAmbush(this))
            {
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
                }
            }
        }

        protected override void OnUpgrade()
        {
            // 质变升级：生成的猎物全部升级（1 伤 → 3 伤），张数固定 3 张
        }
    }
}