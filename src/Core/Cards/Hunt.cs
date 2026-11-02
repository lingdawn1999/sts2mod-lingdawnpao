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
    /// 区域已满 / 冷却时无可埋位置，打出即空放（与「设伏」一致，不做额外保护）。
    /// **结算顺序**：先带出区域里的**旧牌**，最后才填猎物——刚生成的猎物不会被当场带出打掉
    /// （2026-09-23 用户定案，详见 `AmbushZone.BringOutBeforeGenerateAsync`）。
    /// </summary>
    public sealed class Hunt : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Prey>(upgrade: IsUpgraded),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new RepeatVar(3),
        ];

        public Hunt()
            : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

            AmbushZoneState zone = AmbushZone.For(Owner);
            // 结算顺序（2026-09-23 用户定案）：先带出区域里的**旧牌**，最后才填猎物。
            // 遗物钩子对本卡整张豁免，带出必须由本卡自己发起（详见 AmbushZone.BringOutBeforeGenerateAsync）。
            await AmbushZone.BringOutBeforeGenerateAsync(choiceContext, cardPlay.Target, Owner);
            if (!AmbushZone.CanContinueCombat(Owner)) return;

            // 冷却（过载代价）时埋不进任何牌，先判掉，避免凭空造出无处安放的衍生牌
            for (int i = 0; i < DynamicVars.Repeat.IntValue; i++)
            {
                if (!AmbushZone.CanContinueCombat(Owner) || zone.IsFull || zone.IsCoolingDown)
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

            // 埋伏补充手牌资源；基础生成的3张猎物仍只进入伏击区域。
            if (AmbushZone.IsVeteranAmbush(this))
            {
                AmbushEffects.Record(this);
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    if (!AmbushZone.CanContinueCombat(Owner)) break;
                    await Prey.CreateIntoHandAsync(Owner, CombatState!, IsUpgraded);
                }
            }
        }

        protected override void OnUpgrade()
        {
            // 质变升级：生成的猎物全部升级（1 伤 → 3 伤），张数固定 3 张
        }
    }
}