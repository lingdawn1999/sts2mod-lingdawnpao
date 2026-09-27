using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 过载：**0 费**罕见技能（**自带[gold]虚无[/gold]**）。本回合伏击区内的牌视为所有类型
    /// （每张同时提供力量与敏捷）；代价是下回合整个伏击区失效（不提供增益、不能埋牌、不能带出）。
    /// **升级：去掉[gold]虚无[/gold]**（照本体 Apparition / EchoForm 的写法 `RemoveKeyword(CardKeyword.Ethereal)`）。
    /// **沿革（2026-09-21）**：原为"1 费 + 升级获得[gold]保留[/gold]"——全池横向体检发现「保留」浓度
    /// 达本体的 3.6 倍（8~9 张 vs 本体 3.2%）后，用户裁定改为"**0 费 虚无**、升级去掉虚无"：
    /// 把"攥在手里等伏击区堆满"的语义从**保留**换成**虚无的反面**（虚无逼你当回合处理掉，升级即解除），
    /// 既降了费、又把一张保留换成了本体里同样稀少但 MOD 缺席的 Ethereal。
    /// **2026-09-22**：当天试过两版"按上限给满力/敏"的加强（整体给满 → 只给多数派一侧），
    /// 用户最终判定那套设计"拧巴且空区域白拿"，**整张还原回本版**——效果、费用、虚无全部回到上面的口径。
    /// </summary>
    public sealed class Overload : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<OverloadPower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

        public Overload()
            : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

            AmbushZoneState zone = AmbushZone.For(Owner);
            zone.IsOverloaded = true;
            await PowerCmd.Apply<OverloadPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            Log.Info($"伏击区域：过载，{zone.Count} 张牌视为所有类型");
            await AmbushZone.RecomputeBonusesAsync(choiceContext, Owner);
            Owner.GetRelic<FrostFang>()?.Flash();
            AmbushZoneUi.Refresh();
        }

        protected override void OnUpgrade()
        {
            RemoveKeyword(CardKeyword.Ethereal); // 升级去掉虚无（原为 AddKeyword(CardKeyword.Retain)）
        }
    }
}