using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 动物交谈药水：**罕见（Uncommon）· 战斗内 · 自身**药水（2026-09-22 用户定稿）。
    /// 随机将 **3** 张[gold]动物+[/gold]加入手牌。
    /// 动物池复用「动物朋友」`AnimalFriendsPower.Animals`（10 只 Token 衍生牌）。
    /// "动物+" = 生成后立即 `CardCmd.Upgrade`（同「动物朋友」升级版的口径）。
    /// 随机源 = `RunState.Rng.CombatCardSelection`（战斗内选牌流，联机一致）。
    /// 定价锚点：罕见档，给 3 张升级版动物（含垃圾件，期望值约 2 张正向件）。
    /// </summary>
    public sealed class SpeakWithAnimals : PotionModel
    {
        public override PotionRarity Rarity => PotionRarity.Uncommon;

        public override PotionUsage Usage => PotionUsage.CombatOnly;

        public override TargetType TargetType => TargetType.Self;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(3),
        ];

        protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
        {
            var rng = Owner.RunState.Rng.CombatCardSelection;
            int count = DynamicVars.Cards.IntValue;
            var generated = new List<CardModel>(count);

            for (int i = 0; i < count; i++)
            {
                CardModel? canonical = rng.NextItem(AnimalFriendsPower.Animals);
                if (canonical == null) continue;

                CardModel animal = Owner.Creature.CombatState!.CreateCard(canonical, Owner);
                CardCmd.Upgrade(animal); // 动物+
                generated.Add(animal);
            }

            if (generated.Count > 0)
            {
                await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, Owner);
            }
        }
    }
}
