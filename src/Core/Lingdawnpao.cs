using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 芝士雪豹。卡池/遗物池/药水池均已自有；
    /// 局内 Spine 形象与火堆/商人动画仍借用铁甲的（待 BaseLib 迁移时替换）。
    /// </summary>
    public sealed class Lingdawnpao : CharacterModel
    {
        public override CharacterGender Gender => CharacterGender.Neutral;

        protected override CharacterModel? UnlocksAfterRunAs => null;

        public override Color NameColor => new Color("3EB3ED");

        public override Color EnergyLabelOutlineColor => new Color("151D25");

        public override int StartingHp => 72;

        public override int StartingGold => 99;

        public override CardPoolModel CardPool => ModelDb.CardPool<LingdawnpaoCardPool>();

        public override RelicPoolModel RelicPool => ModelDb.RelicPool<LingdawnpaoRelicPool>();

        public override PotionPoolModel PotionPool => ModelDb.PotionPool<LingdawnpaoPotionPool>();

        public override IEnumerable<CardModel> StartingDeck =>
        [
            ModelDb.Card<ClawSwipe>(),
            ModelDb.Card<ClawSwipe>(),
            ModelDb.Card<ClawSwipe>(),
            ModelDb.Card<ClawSwipe>(),
            ModelDb.Card<FurGuard>(),
            ModelDb.Card<FurGuard>(),
            ModelDb.Card<FurGuard>(),
            ModelDb.Card<FurGuard>(),
            ModelDb.Card<SneakAttack>(),
            ModelDb.Card<HuntOperation>(),
        ];

        public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<FrostFang>()];

        public override float AttackAnimDelay => 0.15f;

        public override float CastAnimDelay => 0.25f;

        /// <summary>
        /// 全部动画触发器压到 idle_loop 常态：现有骨架的 attack/cast/hurt/die 动画质量不佳，
        /// 重做 Spine 之前所有动作（攻击/施法/受击/死亡/蓄力）一律沿用常态。
        /// <para>
        /// **刻意不是 override**：`CharacterModel.GenerateAnimator` 在两个分支上不是同一套重载
        /// （public 是 1 参，public-beta 换成 2 参 `(controller, creature)`，而 override 必须逐字匹配签名）
        /// → 改由 Patches.cs 的 `CharacterModel_GenerateAnimator_LingdawnpaoPatch` 用前缀直接顶掉本体实现
        /// （两个重载都探测、哪个存在挂哪个），所以这里也**不需要** base。
        /// </para>
        /// </summary>
        internal static CreatureAnimator CreateIdleAnimator(MegaSprite controller)
        {
            AnimState idle = new("idle_loop", isLooping: true);
            CreatureAnimator animator = new(idle, controller);
            animator.AddAnyState("Idle", idle);
            animator.AddAnyState("Dead", idle);
            animator.AddAnyState("Hit", idle);
            animator.AddAnyState("Attack", idle);
            animator.AddAnyState("Cast", idle);
            animator.AddAnyState("PowerUp", idle);
            animator.AddAnyState("Relaxed", idle);
            return animator;
        }

        protected override string IconPath => SceneHelper.GetScenePath("ui/character_icons/lingdawnpao_icon");

        public override List<string> GetArchitectAttackVfx() =>
        [
            "vfx/vfx_attack_blunt",
            "vfx/vfx_heavy_blunt",
            "vfx/vfx_attack_slash",
            "vfx/vfx_bloody_impact",
            "vfx/vfx_rock_shatter",
        ];
    }
}
