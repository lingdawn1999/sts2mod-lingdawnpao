using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 决堤：0 费先古（Ancient）技能，「倾泄」的先古版本（对标本体 痛击→破击 的先古形态关系）。
    /// 获取途径 = 本体先古遗物「古老牙齿」ArchaicTooth 把牌组里的初始倾泄转换而来
    /// （映射补丁见 Patches.ArchaicTooth_TranscendenceUpgrades_DamBreakPatch；转换保留升级与附魔；
    /// 尘封魔典的 where 子句自动排除转换表先古卡，所以仍必给狩猎形态）。
    /// 效果：带出伏击区域中的所有牌并依次触发；**每带出一张，获得 4 点格挡**（随带出逐张结算——
    /// 与倾泄"打出前按区域张数一笔算"刻意不同：先带出的牌效果先生效，格挡逐张累计）。
    /// **不带消耗**（倾泄自带消耗，先古版去掉——用户明示），可反复打出；升级只加保留（数值不变，
    /// 本体 ReaperForm/NightPatrol 的 AddKeyword(CardKeyword.Retain) 先例）。
    /// 实现照 AmbushZone.BringOutAllAsync 的循环骨架（冷却/空区时 BringOutFirstAsync 返回 null 必须跳出），
    /// 只是把"一口气带完"拆成"每带出一张就结算一次格挡"。
    /// **2026-09-23 描述改写（用户定案）**：现在**与「倾泄」逐字同模板**，只换数值——
    /// 「倾泄」是"每有一张牌获得 2 点、然后带出"，本卡是"每有一张牌获得 4 点、然后带出"，
    /// 玩家一眼能看出这是同一个形状的先古上位版。
    /// 沿革：最早写作"每打出一张牌，获得X点格挡，并带出……"，会被读成"之后你每打出一张**手牌**都触发"
    /// （像持续型能力），语义不清，故先改成"每带出一张"，再按用户要求统一到倾泄的句式。
    /// ⚠️ **文案与实现的唯一差异（有意接受的简化）**：文案读起来是"先按区域张数一笔算格挡、再带出"，
    /// 而实现是**逐张交错**（带出一张→结算 4 点→再带下一张）。两者**总量恒等**（都是区域张数 × 4），
    /// 差别只在"格挡到手的时点"（区域里的牌若在带出过程中改变区域，理论上有细微差别，实战可忽略）。
    /// 若哪天要让卡面**实时显示合计**（像倾泄的 `{InCombat:…CalculatedBlock…}`），才需要改成
    /// `CalculationBaseVar/CalculationExtraVar/CalculatedBlockVar` 三件套。
    /// </summary>
    public sealed class DamBreak : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(4m, ValueProp.Move),
        ];

        public DamBreak()
            : base(0, CardType.Skill, CardRarity.Ancient, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            // 逐张带出：每成功带出并打出一张，立刻结算 4 点格挡，再带下一张。
            // BringOutFirstAsync 返回 null（区域空/冷却中）即结束，同 BringOutAllAsync 的防死循环写法。
            while (await AmbushZone.BringOutFirstAsync(choiceContext, cardPlay.Target, Owner) != null)
            {
                await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            }
        }

        protected override void OnUpgrade()
        {
            AddKeyword(CardKeyword.Retain); // 升级：获得保留（数值不变）
        }
    }
}