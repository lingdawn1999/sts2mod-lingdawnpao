using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 观星：**1 费无色卡（Uncommon）能力**。
    /// 在你的回合开始时，**查看你[gold]抽牌堆[/gold]顶的 5 张牌，你可以将其中的任意张放进[gold]弃牌堆[/gold]**
    /// （没选中的牌**保持原顺序留在牌堆顶**）。**升级：获得[gold]固有[/gold]**（`AddKeyword(CardKeyword.Innate)`）。
    /// **⚠️ 用户原本想用"预见 5"这个词条，查证后改口径**：本体 `card_keywords.json` 里只有
    /// 永恒/虚无/消耗/固有/保留/奇巧/不能被打出，**没有"预见"这个机制**（中英搜 `预见`/`Scry`/`Foresee` 均无匹配），
    /// 所以**不造词条、直接照效果描述**（用户 2026-09-21："那就不用预见当词条了，改成准确描述效果。"）。
    /// **实现要点**：`PileType.Draw.GetPile(owner).Cards` 是 `IReadOnlyList`，**索引 0 就是牌堆顶**
    /// （见本体 [CardPileCmd.Draw](file:///d:/sts2mod/sts2mod-unciphered/src/Core/Commands/CardPileCmd.cs) 里 `drawPile.Cards.FirstOrDefault()` 抽牌），
    /// 所以取顶 5 张 = `Cards.Take(5)`；再走 `CardSelectCmd.FromCombatPile` + `filter: c => top.Contains(c)`
    /// 把选择范围限死在这 5 张里，最后 `CardCmd.Discard`。
    /// 选择提示用**本体内置的 `CardSelectorPrefs.DiscardSelectionPrompt`**（不需要自己写 `selectionScreenPrompt` 键，
    /// 顺带避开翻雪踩过的"缺键抛异常"坑）；`min = 0` 保证可以一张都不弃（确定键直接过）。
    ///
    /// **2026-09-22 改为无色卡**（用户原话："把观星改成无色卡，让他以无色卡的方式被获取（商店）
    /// 然后开放给所有角色都能获取。（因为它和我们的风格有点不搭，开放出来算了）"）。三处配套改动：
    ///   ① **从 `LingdawnpaoCardPool` 移出、挂进 `ColorlessCardPool`**
    ///      （`ModInitializer` 里 `ModHelper.AddModelToPool&lt;ColorlessCardPool, Stargazing&gt;()`）。
    ///      **商店卖无色卡的口径**：`MerchantInventory` 第 116 行就是
    ///      `ModelDb.CardPool&lt;ColorlessCardPool&gt;().GetUnlockedCards(...)`，所以挂进无色池 = 商店能刷到；
    ///      卡牌百科的"无色"筛选也是 `c.Pool is ColorlessCardPool`，会自动归类过去。
    ///      **必须从雪豹池移除**：`CardModel.Pool` 是
    ///      `ModelDb.AllCardPools.FirstOrDefault(p =&gt; p.AllCardIds.Contains(Id))`——**取第一个命中的池**，
    ///      两边都挂会解析到雪豹池，等于没改。
    ///   ② **不再实现 `ILingdawnpaoCard`**：那个标记接口被 `LingdawnpaoOwnCards.IsOwn` 用来把卡框染浅冰蓝、
    ///      把费用图标换成雪花。观星既然是无色卡，就该吃无色框 + 无色能量图标，不能两套样式混着来。
    ///   ③ **`PortraitPath` 必须 override**：本体是
    ///      `atlases/card_atlas.sprites/{Pool.Title}/{id}.tres`，换池后 `Pool.Title` 变成 "colorless"，
    ///      会去 `colorless/` 目录找卡面（我们没往那儿放 → 回落成缺失图）。故卡面路径钉死回雪豹目录。
    ///      ⚠️ **但那个 `.tres` 文件本体从来不会去读，不需要存在**（2026-09-23 核清，别被"目录是空的"骗到）：
    ///      本体 `AtlasResourceLoader._Load` 只用正则 `res://images/atlases/([^/]+)\.sprites/(.+)\.tres`
    ///      **解析路径字符串**，拿 `card_atlas` 图集里找 sprite，找不到再走
    ///      `GetCardFallbackPath` → `res://images/packed/card_portraits/{SpriteName}.png`。
    ///      所以只要 `packed/card_portraits/lingdawnpao/stargazing.png` 在，卡面就正常；
    ///      `images/atlases/card_atlas.sprites/` 是**空目录也没关系**（实测核对过全池：112 个卡牌 key
    ///      ↔ 112 张卡面 PNG 一一对应，无缺失）。**别因为"图集目录是空的"就删掉这里的 override**——
    ///      删了才会真的回落成缺失图。
    /// </summary>
    public sealed class Stargazing : CardModel
    {
        /// <summary>卡面仍放在雪豹目录（见类注释 ③）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/stargazing.tres";

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<StargazingPower>(),
        ];

        public Stargazing()
            : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<StargazingPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            AddKeyword(CardKeyword.Innate);   // 升级给固有（照本体无限刀刃的升级口径）
        }
    }
}