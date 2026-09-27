using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 野生狗奶：**2 费金卡（Rare）能力**（2026-09-23 用户定稿）。玩梗卡。
    /// 你的[gold]埋伏[/gold]牌**加入[gold]伏击区域[/gold]时立刻解锁[gold]埋伏[/gold]效果**
    /// （即不再需要跨回合潜伏：埋入当回合带出就能吃到埋伏收益）。
    /// 卡面文案（2026-09-23 用户改写）：原版"埋入后即可带出，并获得埋伏收益"语义含糊——
    /// 玩家会误解成"这张卡让我的牌能提前带出"，但**带出本来就不需要等待**，
    /// 等待只卡"埋伏效果是否解锁"（见 `AmbushZone.IsReady`）。故改为直述解锁条件。
    /// 升级：费用 2 → 1（效果不变）。
    /// <para>
    /// **定位与代价**：它免掉的正是 2026-09-23 埋伏大改刻意加上的"等一回合"（见
    /// `AmbushZone.IsVeteranAmbush` 的说明），等于把那次堵死的"同回合埋入即兑现"通路
    /// **改成付费解锁**。净能量不变（埋入照付、带出免费），换来的是节奏：
    /// 一回合内埋 → 切形态 → 带出一气呵成。也因此它是一张纯放大器——埋伏数值是
    /// 按"有等待期"定过价的，故定价压到 2 费金卡，不再往下让。
    /// </para>
    /// <para>
    /// **已知副作用**：能力在场时区域里所有埋伏牌会立刻全亮金框（金框查的就是
    /// `IsReadyForAmbush`），"金框 = 就绪"这个信号会失效。用户 2026-09-23 已接受。
    /// </para>
    /// <para>
    /// **「永恒」词条 = 梗的机制化**（用户 2026-09-23 要求）：梗本源是抖音博主"吴川才子招茂才"的整蛊
    /// 道具包装（「粤西特供」+「野生狗奶」+ 保质期标"永久"），奶奶因名字离谱当场拒喝，网友据此把它
    /// 玩成"永恒 / 不朽"的代名词（"万物皆有落幕，唯有狗奶万古长存"）。
    /// 本体关键词「永恒」的机制是**无法从你的牌组中移除或变化**（`CardModel.IsRemovable`），
    /// 所以挂上它之后这张牌会**永久留在牌组里——商店删不掉、事件也变不掉**，正是"万古长存"的机制双关。
    /// ⚠️ 这是**有意为之的真负面**（一张 2 费能力永久占位），不是漏配的 bug，别"顺手"去掉。
    /// </para>
    /// <para>
    /// ⚠️ 卡面文字（「野生狗奶」「雪原特供」）是**后期合成**上去的，不是模型生成——
    /// 原参考图是别人的商品实拍照片（版权风险），故只沿用梗的文字与版式，画面完全自绘。
    /// **文字是梗的全部，不要"顺手"改字。**
    /// </para>
    /// </summary>
    public sealed class WildDogMilk : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/wild_dog_milk.tres";

        /// <summary>
        /// 「永恒」：无法从你的[gold]牌组[/gold]中移除或变化（见类注释——这是梗的机制化，真负面、有意为之）。
        /// </summary>
        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Eternal];

        public WildDogMilk()
            : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<WildDogMilkPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 2 费 → 1 费（效果不变）
        }
    }
}