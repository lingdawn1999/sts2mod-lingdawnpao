using MegaCrit.Sts2.Core.Entities.Cards;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 奇袭：偷袭的先古版本，1费，造成8（12）伤害，给予1（2）虚弱。
    /// 埋伏再给予1（2）虚弱，造成8（12）伤害。
    /// 沿用旧决堤的类名和卡牌ID，保证旧档能读取；古老牙齿改为转换偷袭。
    /// </summary>
    public sealed class DamBreak : SneakAttack
    {
        protected override decimal InitialDamage => 8m;
        protected override decimal InitialAmbushDamage => 8m;

        public override string PortraitPath =>
            "res://images/packed/card_portraits/lingdawnpao/dam_break.png";

        public DamBreak() : base(1, CardRarity.Ancient)
        {
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(4m);
            DynamicVars["AmbushDamage"].UpgradeValueBy(4m);
            DynamicVars["WeakPower"].UpgradeValueBy(1m);
            DynamicVars["AmbushWeak"].UpgradeValueBy(1m);
        }
    }
}
