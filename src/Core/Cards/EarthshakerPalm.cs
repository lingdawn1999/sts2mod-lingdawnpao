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
    /// 每段 3 点伤害；**升级把段数 2 → 3**（总伤 6 → 9）。
    /// 写法对标本体「匕首雨」DaggerSpray（1 费白 · 全体 · 4 伤 ×2 段，见
    /// sts2mod-unciphered/src/Core/Models/Cards/DaggerSpray.cs）：
    /// `DamageCmd.Attack(...).WithHitCount(n).TargetingAllOpponents(...)`。
    /// **段数走 DynamicVar 而不是写死常量**：本体 DaggerSpray 的升级是加伤害、段数不变，所以它敢把
    /// "两次"直接写进文案；本卡升级改的是**段数**，文案必须跟着动，故用 `Hits` 动态值 +
    /// `{Hits:diff()}` 占位符（同 `FrostThrone` 的 `{FrostPerEnergy:diff()}` 写法）。
    /// **数值沿革**：初版 3 段 × 2（升级每段 +1 → 3×3）→ **2026-09-22 用户削弱为 2 段 × 3
    /// （升级段数 +1 → 3 段 × 3）**：总伤同为 6/9，但**段数少一段 = 少吃一次力量**，
    /// 这是"削弱"的实际落点（多段攻击逐段吃力量，同 FiendFire 规则）。
    /// </summary>
    public sealed class EarthshakerPalm : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(3m, ValueProp.Move),
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
            DynamicVars["Hits"].UpgradeValueBy(1m); // 段数 2 → 3
        }
    }
}