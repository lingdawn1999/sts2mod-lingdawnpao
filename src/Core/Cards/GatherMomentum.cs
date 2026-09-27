using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 蓄势：伏击区域内每张牌提供的增益 +1（攻击牌力量、技能牌敏捷）。
    /// 稀有 1 费能力。打出后立即对已埋的牌追溯生效（重算模型全量差分）。
    /// 对比本体 DemonForm（3 费稀有 每回合+2力）：本卡不自己产力，而是放大伏击区的
    /// 既有收益，需要先埋牌才兑现，费用 1 合理；上限受伏击区 3~5 张约束，不无脑。
    /// **2026-09-25 用户重定升级位：升级改为获得「固有」**（原为"加值 +1 → +2"）——
    /// 按"降费 / 数值二选一"的约定，升级不再放大数值；加值固定 +1。
    /// **2026-09-27 用户再改：升级改回降费 1 → 0**（替代固有）——0 费后随时能补挂，可及性收益比开局在手更实用。
    /// </summary>
    public sealed class GatherMomentum : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new RepeatVar(1),
        ];

        public GatherMomentum()
            : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            AmbushZoneState zone = AmbushZone.For(Owner);
            zone.BonusExtra += DynamicVars.Repeat.IntValue;
            Log.Info($"伏击区域：每张埋牌增益提升至 +{1 + zone.BonusExtra}（蓄势）");
            await AmbushZone.RecomputeBonusesAsync(choiceContext, Owner);
            Owner.GetRelic<FrostFang>()?.Flash();
            AmbushZoneUi.Refresh();
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 1 → 0 费（2026-09-27 用户定案，替代原"固有"）
        }
    }
}
