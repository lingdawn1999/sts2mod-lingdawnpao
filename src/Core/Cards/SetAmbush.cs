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
    /// 设伏：1 费普通技能，直接生成 2 张「暗袭」埋入伏击区域，是伏击流的快速启动件
    /// （普通埋牌要花牌本身的能量且占当回合节奏，本卡绕过这两点）。
    /// 暗袭是 0 费衍生攻击（5 伤、自带消耗），靠带出免费兑现，给「倾泄」「猛虎连击」喂弹药。
    /// **升级（2026-09-23 用户改）**：从"生成的暗袭全部为升级版（5→7 伤）"改成 **张数 2 → 3**。
    /// 口径是"**买数量、不买质量**"：升级后是 3 张普通暗袭（潜在 15 伤）而不是 2 张升级版（潜在 14 伤）——
    /// 总伤基本持平，**换来的是一格伏击区存量**：区域力敏多一层，且「收网」「霜牙」「倾泄」
    /// 这些按区域张数结算的牌都吃这一格。故生成的暗袭**不再随升级变升级版**（否则两张同时吃是双重升级）。
    /// 定位差：设伏是纯铺格；「扑击」是 1 费 3 伤 + 埋抽牌堆顶 1 张真牌（带输出、压缩牌组，但只铺一格）。
    /// 区域已满则停止生成，不会浪费。
    /// **结算顺序（v2）**：直接生成暗袭——v2 普通出牌不再自动带出，埋进去就老实待着。
    /// </summary>
    public sealed class SetAmbush : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<AmbushStrike>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new RepeatVar(2),
        ];

        public SetAmbush()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

            AmbushZoneState zone = AmbushZone.For(Owner);
            // 先带出区域旧牌，再生成新牌（避免带出刚生成的牌，见 AmbushZone 带出顺序约定）
            await AmbushZone.BringOutBeforeGenerateAsync(choiceContext, cardPlay.Target, Owner);
            if (!AmbushZone.CanContinueCombat(Owner)) return;

            for (int i = 0; i < DynamicVars.Repeat.IntValue; i++)
            {
                if (!AmbushZone.CanContinueCombat(Owner) || zone.IsFull || zone.IsCoolingDown)
                {
                    break;
                }

                // 不再按升级与否改暗袭的质量（升级买的是张数，见类注释）
                AmbushStrike strike = CombatState!.CreateCard<AmbushStrike>(Owner);
                await AmbushZone.BuryGeneratedAsync(choiceContext, Owner, strike);
                await Cmd.Wait(0.1f);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Repeat.UpgradeValueBy(1m); // 张数 2 → 3
        }
    }
}
