using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪崩：2 费金卡攻击，**自带保留**，对敌方全体结算——
    /// 对每个敌人造成"**其冰冻层数 × 2**"点伤害（升级 × 3），**打完后清空该敌人的全部冰冻**。
    /// **保留的意义**：层数是存量、雪崩把它兑现成一次流量，所以这卡要"**等层数堆高了再放**"；
    /// 保留保证它不会在自己还小的时候被随手丢掉（若不清层，配合保留会变成每回合白嫖的永动机——
    /// 2026-09-21 用户判定层数会越叠越快、那样数值无上限，故定为"保留 + 清层"）。
    /// 清层在伤害**之后**：这次伤害仍带满层数走一遍处决判定（详见 OnPlay 注释）。
    /// 逐敌各算各的层数（不是把全场层数汇总成一段）；没有冰冻的敌人跳过。
    /// **先对 `HittableEnemies` 取快照再遍历**：伤害可能打死敌人，直接遍历活体集合会在中途被改。
    /// </summary>
    public sealed class Avalanche : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new RepeatVar(2),
        ];

        /// <summary>自带保留：层数每回合都在涨，留住它才能在层数高了之后反复开火。</summary>
        public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

        public Avalanche()
            : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            List<Creature> enemies = CombatState!.HittableEnemies.ToList();
            foreach (Creature enemy in enemies)
            {
                if (!enemy.IsAlive)
                {
                    continue;
                }

                FrostbitePower? frost = enemy.GetPower<FrostbitePower>();
                int layers = frost == null ? 0 : (int)frost.Amount;
                if (layers <= 0)
                {
                    continue;
                }

                await DamageCmd.Attack(layers * DynamicVars["Repeat"].BaseValue).FromCardCompat(this, cardPlay)
                    .Targeting(enemy)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);

                // 兑现：打完**先让伤害结算，再清空该敌人的层数**。
                // 顺序刻意如此——清层在伤害之后，于是这次伤害仍带着满层数走一遍
                // `AfterDamageReceived` 的处决判定：敌人若被打到"生命值 < 冰冻层数"就当场冻死
                // （伤害 2N，故 2N < HP < 3N 的敌人会被处决；升级 3N 时是 3N < HP < 4N）。
                // 想要"只砸不处决"就把下面这段挪到 DamageCmd 之前。
                if (frost != null && enemy.IsAlive)
                {
                    await PowerCmd.Remove(frost);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars["Repeat"].UpgradeValueBy(1m); // 2 倍 → 3 倍
        }
    }
}