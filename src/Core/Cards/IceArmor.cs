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
    /// 雪隐：1 费**金卡（Rare）能力**。① 每当你给予敌人[gold]冰冻[/gold]，获得 2 点[gold]格挡[/gold]；
    /// ② 你受到攻击而没有失去生命值时，给予所有敌人 1 层[gold]冰冻[/gold]。
    /// **升级同时抬两项**：① 格挡 2 → 3、② 反挂冰 1 → 2（用户 2026-09-21："升级变成3防御，给敌方全体挂2冰冻"）。
    /// **形态沿革（四次，最终定案——别再改）**：① 初版"1 费技能 + 本回合覆甲"；② 一度改成"能力牌 + 蓝卡"；
    /// ③ 同日又改回"技能牌 + 本回合"（`IceArmorPower` 加过 `AfterSideTurnEnd` 自解）；
    /// ④ **随即用户改判："雪隐做成能力也可以，但是得升金卡了"**——因为两条效果一旦**常驻**，
    /// 强度明显高于蓝卡档，于是**就地升为金卡（Rare）**、形态回到能力牌。
    /// **最终形态 = 能力牌 + 金卡 + 两条常驻效果**（`IceArmorPower` 的自解钩子已移除）。
    /// ② 形成"挡得住 → 反挂冰 → 挂冰又给格挡"的**自循环**，只有被攻击时才推进，强度受"这回合被攻击几次"天然限制。
    /// **不带消耗**：每回合都能再挂一次，强度受"抽到几张"天然限制。
    /// </summary>
    public sealed class IceArmor : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<IceArmorPower>(2m),
            new DynamicVar("FrostbiteStacks", 1m),
        ];

        public IceArmor()
            : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            IceArmorPower? armor = await PowerCmd.Apply<IceArmorPower>(choiceContext, Owner.Creature,
                DynamicVars["IceArmorPower"].BaseValue, Owner.Creature, this);

            // ② 的层数走"卡注入"：能力只收得到一个 amount（那是 ① 的格挡值），
            // 所以第二个数值由卡在施加后写进能力（各端都会执行 OnPlay，注入结果一致）。
            if (armor != null)
            {
                armor.FrostbiteStacks = DynamicVars["FrostbiteStacks"].BaseValue;
            }

            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            DynamicVars["IceArmorPower"].UpgradeValueBy(2m);    // ① 格挡 2 → 4（2026-09-24 升级收益 +1 → +2）
            DynamicVars["FrostbiteStacks"].UpgradeValueBy(1m);  // ② 反挂冰 1 → 2
        }
    }
}