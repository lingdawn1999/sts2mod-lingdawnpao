using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 伏击艺术：**金（Rare）· 战斗内 · 自身**药水（2026-09-21 用户定稿："第一个：伏击艺术，
    /// 立即用随机牌填满伏击区域（从自己的卡池+无色牌里抽）。"）。
    /// 效果：**把[gold]伏击区域[/gold]用随机牌填满**——牌源 = **mod 自己的卡池**（`LingdawnpaoCardPool`）
    /// **∪ 本体无色牌池**（`ColorlessCardPool`），排除 Token 与不可打出的牌。
    /// **填满逻辑整段照「捕猎」`Hunt`**：`while (!zone.IsFull &amp;&amp; !zone.IsCoolingDown)` →
    /// `CreateCard(canonical, owner)` → `AmbushZone.BuryGeneratedAsync(...)`（内部自己会再判一次满/冷却，
    /// 返回 false 即 break），每次 `Cmd.Wait(0.1f)` 做出埋牌节奏。**区域满 / 冷却时用掉即空放**（与「捕猎」一致）。
    /// **随机源** = `RunState.Rng.CombatCardSelection`（战斗内随机选牌流，同「动物朋友」），联机各端一致。
    /// **为什么是金档**：一次性把 3~5 格全填上是"多张免费牌"级收益（喂「收网」「霜牙」「有备而来」的区域存量收益），
    /// 且不受手牌限制；对照本体同档位「瓶中幽灵」GhostInAJar（金：1 层无实体）。
    /// </summary>
    public sealed class AmbushArt : PotionModel
    {
        public override PotionRarity Rarity => PotionRarity.Rare;

        public override PotionUsage Usage => PotionUsage.CombatOnly;

        public override TargetType TargetType => TargetType.Self;

        public override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
        {
            AmbushZoneState zone = AmbushZone.For(Owner);

            while (!zone.IsFull && !zone.IsCoolingDown)
            {
                CardModel? canonical = Owner.RunState.Rng.CombatCardSelection.NextItem(RandomPool);
                if (canonical == null)
                {
                    break;
                }

                CardModel card = Owner.Creature.CombatState!.CreateCard(canonical, Owner);
                if (!await AmbushZone.BuryGeneratedAsync(choiceContext, Owner, card))
                {
                    break;
                }

                await Cmd.Wait(0.1f);
            }
        }

        /// <summary>牌源：mod 卡池 ∪ 本体无色牌池，排除 Token 与不可打出的牌（避免埋进打不出来的东西）。</summary>
        private static IEnumerable<CardModel> RandomPool =>
            ModelDb.CardPool<LingdawnpaoCardPool>().AllCards
                .Concat(ModelDb.CardPool<ColorlessCardPool>().AllCards)
                .Where(c => c.Rarity != CardRarity.Token && !c.Keywords.Contains(CardKeyword.Unplayable));
    }
}