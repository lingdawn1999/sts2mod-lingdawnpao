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
    /// 战斗使用独立雪豹骨架，支持常规与抱树丛低伏两套待机。
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

        // 2026-10-28：自然的护佑从开局自带挪进白色遗物池（LingdawnpaoSurvival.Rarity → Common），
        // 开局保底由伏击者的"首回合自选入区"（FrostFang.AfterPlayerTurnStart）承担。
        public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<FrostFang>()];

        public override float AttackAnimDelay => 0.266667f;

        public override float CastAnimDelay => 0.25f;

        /// <summary>
        /// 新骨架使用常规与埋伏两套待机，并提供攻击、受击、施法和终止的死亡状态。
        /// 旧骨架缺少埋伏动画时仍可沿用占位状态。
        /// <para>
        /// **刻意不是 override**：`CharacterModel.GenerateAnimator` 在两个分支上不是同一套重载
        /// （public 是 1 参，public-beta 换成 2 参 `(controller, creature)`，而 override 必须逐字匹配签名）
        /// → 改由 Patches.cs 的 `CharacterModel_GenerateAnimator_LingdawnpaoPatch` 用前缀直接顶掉本体实现
        /// （两个重载都探测、哪个存在挂哪个），所以这里也**不需要** base。
        /// </para>
        /// </summary>
        internal static CreatureAnimator CreateIdleAnimator(MegaSprite controller)
        {
            if (controller.HasAnimation("ambush_idle"))
                return LingdawnpaoAnimationController.Create(controller,
                    LingdawnpaoAnimationController.FindCreature(controller));
            AnimState idle = new("idle_loop", isLooping: true);
            CreatureAnimator animator = new(idle, controller);
            animator.AddAnyState("Idle", idle);
            // Keep the same placeholder pose, but death must finish instead of entering an idle loop.
            animator.AddAnyState("Dead", CreateDeathState());
            animator.AddAnyState("Revive", idle);
            animator.AddAnyState("Hit", idle);
            animator.AddAnyState("Attack", idle);
            animator.AddAnyState("Cast", idle);
            animator.AddAnyState("PowerUp", idle);
            animator.AddAnyState("Relaxed", idle);
            return animator;
        }

        internal static AnimState CreateDeathState() => new("idle_loop", isLooping: false);

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
