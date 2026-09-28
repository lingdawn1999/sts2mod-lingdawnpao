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
    /// 老吴撼地掌：1 费普通攻击，**对敌方全体结算、分 2 段**。
    /// 每段 **4** 点伤害；**升级每段 4 → 6**（总伤 8 → 12）。
    /// 写法对标本体「匕首雨」DaggerSpray（1 费白 · 全体 · 4 伤 ×2 段，见
    /// sts2mod-unciphered/src/Core/Models/Cards/DaggerSpray.cs）：
    /// `DamageCmd.Attack(...).WithHitCount(n).TargetingAllOpponents(...)`。
    /// **段数走 DynamicVar 而不是写死常量**：文案用 `{Hits:diff()}` 占位符
    /// （同 `FrostThrone` 的 `{FrostPerEnergy:diff()}` 写法）。
    /// **数值沿革**：初版 3 段 × 2（升级每段 +1 → 3×3）→ 2026-09-22 用户削弱为 2 段 × 3
    /// （升级段数 +1 → 3 段 × 3）→ **2026-09-28 用户改版：2 段 × 4，升级每段 4 → 6**（总伤 8/12，
    /// 升级从"加段数"改回"加伤害"）。
    /// </summary>
    public sealed class EarthshakerPalm : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(4m, ValueProp.Move), // 2026-09-28：3 → 4
            new DynamicVar("Hits", 2m),
        ];

        public EarthshakerPalm()
            : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .WithHitCount((int)DynamicVars["Hits"].BaseValue).FromCardCompat(this, cardPlay)
                .TargetingAllOpponents(CombatState!)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m); // 每段 4 → 6（2026-09-28：从"段数 2→3"改回加伤害）
        }
    }
}