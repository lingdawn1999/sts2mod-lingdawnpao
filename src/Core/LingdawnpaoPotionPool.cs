using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 芝士雪豹药水池（2026-09-21 用户："解绑铁甲战士的药水池，慢慢加自己的东西"）。
    /// **从 `IroncladPotionPool` 解绑**——角色此前借用铁甲的药水池，现在改为自有池，
    /// 与本体各角色"**3 张专属药水**"的结构对齐（本体角色池都来自各自的 `*4Epoch.Potions`，正好 3 张）。
    /// **共享药水不受影响**：本体的取池方式是把"角色池 ∪ `SharedPotionPool`(45 张)"
    /// （`PotionFactory` / 各事件里都是 `.Concat(ModelDb.PotionPool<SharedPotionPool>())`），
    /// 所以换成自有池后，玩家仍然能拿到全部无色/共享药水，只换掉了那 3 张角色专属。
    /// 现有 2 张（暴雪瓶·白、伏击艺术·金），**蓝档位空着**等同族补充。
    /// </summary>
    public sealed class LingdawnpaoPotionPool : PotionPoolModel
    {
        public override string EnergyColorName => "defect";

        public override Color LabOutlineColor => new Color("3EB3ED");

        protected override IEnumerable<PotionModel> GenerateAllPotions() =>
        [
            ModelDb.Potion<BlizzardBottle>(),
            ModelDb.Potion<SpeakWithAnimals>(),
            ModelDb.Potion<AmbushArt>(),
        ];
    }
}