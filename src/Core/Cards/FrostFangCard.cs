using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 霜牙：1 费金卡（Rare）攻击。2026-09-21 v4 定稿——用户判定 v3（偷袭的先古形态+易伤/虚弱）"步子迈大了，
    /// 效果太多导致和偷袭基本没什么关系"，故：删掉易伤/虚弱、退出先古名额、取消与偷袭的先古形态关系（古老牙齿
    /// 映射补丁已整体移除）。**「偷袭的先古升级版」以后另做一张新卡**，届时复用已验证的做法：
    /// Postfix 本体 `ArchaicTooth.TranscendenceUpgrades` getter 补映射（尘封魔典会自动排除转换表先古卡，
    /// 详见开发进度日志/项目记忆）。现在这张霜牙只是与偷袭同家族（双段+埋伏）的独立金卡。
    /// 效果：造成基础伤害；从伏击区域带出打出时额外补一段埋伏伤害；打出时**消耗伏击区域里自选的一张牌**，
    /// 它的卡面伤害**同时加到两个伤害数字上**（X 双吃：带出打出的总伤 = (基础+X) + (埋伏段+X)）。
    /// 本体同款是 Thrash（1 费金卡：消耗手牌里随机一张攻击牌、把它的伤害加给自己），
    /// 沿用其实现骨架：DamageVar 直接累加 + 私有 _extraDamage 记录增量（供降级复原）。
    /// 两处刻意差别：① 作用对象是"**伏击区域里自选的一张**"而不是手牌里的一张；
    /// ② **只取卡面伤害（BaseValue），不跑 Hook.ModifyDamage**——否则会和伏击区按张数给的 +力量双重叠加
    /// （Thrash 只吃一张所以无所谓，我们一次吃一整片就会失控）。
    /// **结算顺序**：① 弹选牌界面让你挑 → ② 读那张牌的伤害 → ③ `ExhaustChosenAsync` 把它烧掉
    /// （**此时刻意不重算增益**，所以这片区域给的 +力量/+敏捷对两段伤害都生效）→ ④ 打基础段 → ④′ 埋伏段
    /// （带出时才触发，「伏兵」让整段额外触发 N 次）→ ⑤ `RefreshZoneAsync` 清除区域增益。
    /// **负面效果（2026-09-22 用户更正后的口径，2026-09-23 两次改价）**：**只在埋伏段付出**——
    /// 从手牌正常打出：只有基础伤害（外加自选烧牌那段的加伤）；从伏击区域带出打出：**额外一段伤害 + 失去 1 点生命**。
    /// （原实现是两种打出方式都扣血，用户判定错了："正常打是只有伤害，埋伏是额外伤害+扣血"。）
    /// **数值沿革**：初版 6/6（升级 8/8）+ 失去 1 点生命 → 2026-09-23 数值体检时改成"失去 1 点**生命上限**"，
    /// 两段数值同时收到 **3/4（升级 5/6）**；**同日用户又要求把代价回调成扣血**（3/4 数值不动），
    /// 理由（原话）："扣血上限太伤了，原版的亡灵契约师只比其他角色少一点点上限都会因为火堆回的血少而进
    /// 恶性循环，霜牙这张卡还对治疗有要求，这样会同时降低回血和血上限慢性死亡"——
    /// **上限损失不可逆、是慢性债**，而扣血是可运营的当期成本（火堆/药水能补回来），故退回扣血。
    /// **2026-09-25 用户第三次定价**：作用面从"整片区域"收成"**只吞首张**"，
    /// 数值上调为 **5/6（升级 7/8）**——原来一次吞满 3 张能叠出天量伤害、且区域越扩容越离谱，
    /// 收成一张后单卡收益可控，数值也随之抬回"一张金卡攻击该有的基础伤害"。
    /// **同日第四次（用户：加强一下霜牙）**：把烧牌从"固定烧最先加入的那张"放宽为
    /// **玩家自选区域里的一张**（数值不动）——固定首张只是"埋的顺序"决定烧谁，
    /// 玩家没法把想烧的那张（比如伤害最高的那张）挑出来吃伤害。
    /// 写法照本体 `Bloodletting`（放血，1 费：失去 3 点生命换 2 能量）/ `Offering`（祭品）：
    /// `HpLossVar(1m)` 做数值来源 + `CreatureCmd.Damage(..., HpLoss, Unblockable | Unpowered | Move, this)`。
    /// **`Unblockable` 是刻意的**：这是"代价"而不是"受击"，格挡不该能挡掉它（本体全部扣血卡一致）。
    /// ⚠️ 霜牙**自身没有消耗词条**（那个 Exhaust hover tip 只是解释卡面文字里的"消耗你的伏击区域"），
    /// 所以一局里能被反复埋→带出，血量成本会累加；每次都得先付 1 费埋入，代价自限。
    /// ⚠️ 遗物「伏击者」是 `AfterCardPlayed` **自动**带出（FIFO）——埋进去之后，
    /// 什么时候付这笔血由"你打出的下一张牌"决定，不能反悔。
    /// 类名带 Card 后缀避开遗物类 `FrostFang`（遗物显示名「伏击者」，类名保持不变）。
    /// </summary>
    public sealed class FrostFangCard : CardModel, IAmbushBonusCard
    {
        private decimal _extraDamage;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new HpLossVar(1m),
            new DamageVar(5m, ValueProp.Move),
            new DamageVar("AmbushDamage", 6m, ValueProp.Move),
        ];

        private decimal ExtraDamage
        {
            get => _extraDamage;
            set
            {
                AssertMutable();
                _extraDamage = value;
            }
        }

        public FrostFangCard()
            : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // **自选**要烧的那张（2026-09-25 用户要求：从"固定烧最先加入的"放宽为自选）；
            // 区域为空时返回 null，那就只打基础伤害。区域堆是 PileType.None，
            // 走不了 FromCombatPile（非战斗堆会抛），见 AmbushZone.SelectOneAsync 的说明。
            CardModel? swallowed = await AmbushZone.SelectOneAsync(choiceContext, Owner, SelectionScreenPrompt);

            // 读**选中那张**的卡面伤害——烧掉之后那张牌就不在区域里了。
            // X 同时加到两个伤害数字上（X 双吃：带出打出的总伤 = (基础+X) + (埋伏段+X)）。
            decimal added = 0m;
            if (swallowed != null)
            {
                if (swallowed.DynamicVars.ContainsKey("Damage"))
                {
                    added += swallowed.DynamicVars.Damage.BaseValue;
                }
                else if (swallowed.DynamicVars.ContainsKey("CalculatedDamage"))
                {
                    added += swallowed.DynamicVars.CalculatedDamage.Calculate(null);
                }
            }

            if (added > 0m)
            {
                DynamicVars.Damage.BaseValue += added;
                ((DamageVar)DynamicVars["AmbushDamage"]).BaseValue += added;
                ExtraDamage += added;
            }

            if (swallowed != null)
            {
                await AmbushZone.ExhaustChosenAsync(choiceContext, Owner, swallowed);
            }

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            // 埋伏段：只有**跨回合**从伏击区域带出打出时才触发（2026-09-23 埋伏大改，见 IsVeteranAmbush）。
            // **代价也只在这一段里付**（2026-09-22 用户更正）：正常从手牌打出只造成伤害、不扣血；
            // 带出打出才是"额外一段伤害 + 失去 1 点生命"的代价（2026-09-23 曾短暂改成掉上限，同日用户
            // 判定上限损失会造成不可逆的慢性死亡，已退回扣血，见类注释）。
            // 埋伏伤害同日曾上调到 +10，随后用户判定**过强**、改回 +6；2026-09-23 数值体检后连基础段一起收到 3/4（升级 5/6）。
            if (AmbushZone.IsVeteranAmbush(this))
            {
                // Unblockable：扣血是"代价"不是"受击"，格挡挡不掉（本体 Bloodletting / Offering 同款写法）。
                await CreatureCmdCompat.Damage(choiceContext, Owner.Creature, DynamicVars.HpLoss.BaseValue,
                    ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, null, this, cardPlay);

                var ambushDamage = (DamageVar)DynamicVars["AmbushDamage"];
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await DamageCmd.Attack(ambushDamage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(choiceContext);
                }
            }

            await AmbushZone.RefreshZoneAsync(choiceContext, Owner);
        }

        protected override void AfterDowngraded()
        {
            base.AfterDowngraded();
            DynamicVars.Damage.BaseValue += ExtraDamage;
            ((DamageVar)DynamicVars["AmbushDamage"]).BaseValue += ExtraDamage;
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m);          // 5 → 7
            DynamicVars["AmbushDamage"].UpgradeValueBy(2m); // 6 → 8
        }
    }
}
