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
    /// 乱雪纷飞：1 费罕见（蓝）攻击，**随机目标多段**（2026-09-21 用户定稿）。
    /// 随机对敌人造成 2 点伤害 4 次（总 8）；升级**单段伤害 2 → 4**（总 16）。
    /// **为什么必须做**：全池 `TargetType.RandomEnemy` **零匹配**——这是 mod 唯一空缺的"软 AOE"形状，
    /// 群怪时平均摊、单体时正好全额落到同一个人身上（所以单体也不亏）。
    /// 写法照本体「飞剑回旋镖」SwordBoomerang（1 费白 · 随机 · 3 伤 ×3，升级 +1 次，
    /// 见 sts2mod-unciphered/src/Core/Models/Cards/SwordBoomerang.cs）与「连射」Volley
    /// （0 费 X 蓝 · 随机 · 每点能量 10 伤）：`DamageCmd.Attack(...).WithHitCount(...).TargetingRandomOpponents(...)`。
    /// **数值沿革**：初版 4 伤 ×3（总 12，升级走次数 3→4）。
    /// 2026-09-22 第一刀削到 3 伤 ×3（总 9，升级改加单段 +2）——原因是它当时**同时**压过
    /// 「老吴撼地掌」（1 费白 · 全体 · 3 伤 ×2）：**同费下单次伤害更高、段数还更多，两条轴全赢**。
    /// 同日用户注意到本卡是**蓝卡**（撼地掌是白卡），决定把差异做在"段数"上而不是单次伤害：
    /// **2 伤 ×4 段**（总 8）、升级 4 伤 ×4 段（总 16）——单次伤害**低于**撼地掌、段数**高于**它，
    /// 高稀有度换来的强度落在"段数 ×（力量加成）"这条轴上（多段逐段吃力量，同 FiendFire 规则）。
    /// </summary>
    public sealed class SnowFlurry : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(2m, ValueProp.Move),
            new RepeatVar(4),
        ];

        public SnowFlurry()
            : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.RandomEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(DynamicVars.Repeat.IntValue).FromCardCompat(this, cardPlay)
                .TargetingRandomOpponents(CombatState!)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m); // 单段 2 伤 → 4 伤（次数保持 4）
        }
    }
}