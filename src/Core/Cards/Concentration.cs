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
    /// 凝神：2 费金卡技能，**把手牌里的技能牌全变 0 费，代价是本回合伏击区失效**。
    /// 效果：本回合你[gold]手牌[/gold]中所有[gold]技能牌[/gold]的耗能为 0；**本回合你的[gold]伏击区域[/gold]失效**。
    /// 自带[gold]虚无[/gold]；**升级沿革：原为"去掉虚无"，2026-09-24 用户定案改为"降费 2 → 1"**
    /// （跨模组体检发现 mod 蓝金卡升级增量中位只有 1.0，本体/蕾忍/皮卡丘为 2.0~3.0；
    /// 按"降费或数值二选一"规则，本卡无现成数值位，故走降费，[gold]虚无[/gold] 保留）。
    /// **2026-09-23 用户去掉[gold]消耗[/gold]**：原为"消耗 + 虚无"双词条，用户判断代价已由
    /// "本回合伏击区失效"承担，再叠消耗太重，故只留虚无。
    /// **定价沿革**：最初按 1 费 + 限技能牌 + 消耗设计，**用户判定太强**（mod 技能占比过半，
    /// 配 0 费抽牌件能一回合铺满），于是不抄本体「子弹时间」`BulletTime` 的刹车（3 费 · 手牌全部 0 费 ·
    /// 本回合不能抽牌），改用自家机制定价：拿"本回合的伏击区"换——当回合不能埋牌、不能带出、
    /// 也不提供[gold]力量[/gold]/[gold]敏捷[/gold]（即 `AmbushZoneState.IsCoolingDown`，与「过载」同一条状态）。
    /// **2026-09-22 用户再削**：1 费 → **2 费**，并补[gold]虚无[/gold]、升级只解虚无——
    /// 理由是"不玩伏击的雪球流"里这张几乎不吃代价（伏击区本来就空着），1 费换满手技能 0 费太便宜。
    /// **⚠️ 状态机注意**：全项目原本只有 `OverloadPower` 会把 `IsCoolingDown` 置回 false，
    /// 所以本卡自带 `ConcentrationPower`（回合开始时解除冷却 + 自解），与 OverloadPower 同构、互不干扰。
    /// 若将来再出"让伏击区失效"的卡，**照这张的写法配一个自解能力**，别让冷却永久卡死。
    /// </summary>
    public sealed class Concentration : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

        public Concentration()
            : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

            foreach (CardModel card in PileType.Hand.GetPile(Owner).Cards)
            {
                // X 费牌不能被置为 0 费（照本体子弹时间的写法：先排除 CostsX）
                if (card.Type == CardType.Skill && !card.EnergyCost.CostsX)
                {
                    card.SetToFreeThisTurn();
                }
            }

            AmbushZoneState zone = AmbushZone.For(Owner);
            zone.IsCoolingDown = true;
            await PowerCmd.Apply<ConcentrationPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            Log.Info("伏击区域：凝神，本回合失效");
            await AmbushZone.RecomputeBonusesAsync(choiceContext, Owner);
            Owner.GetRelic<FrostFang>()?.Flash();
            AmbushZoneUi.Refresh();
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1);   // 2 费 → 1 费（2026-09-24 用户定案，替代原"去虚无"；虚无保留）
        }
    }
}