using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪球技巧：**1 费罕见（蓝）技能**，自带[gold]消耗[/gold]。**用「雪球」填满你的[gold]伏击区域[/gold]**（填到当前上限，永远是普通雪球）。
    /// **升级 = 去掉[gold]消耗[/gold]**（本战斗可反复打出填区；走 <c>RemoveKeyword</c>，
    /// 读档时本体 <c>FromSerializable</c> 会重放升级、自动重新移除，存档安全）。
    /// 定位：**雪球线 → 伏击线的"输送带"**（两条机制之间的润滑件）。
    /// <para>
    /// **为什么埋区域比塞手牌值**（这是本卡区别于「雪球保护」「打雪仗」的卖点）：
    /// ① 伏击区按牌**类型**给加成——雪球是[gold]攻击牌[/gold]，在区域里每张给 **+1 [gold]力量[/gold]**，
    ///    填满 3 格就是一座稳定的力量平台（「扩张」「暗杀者」把上限抬到 4 时更高）；
    /// ② 带出时是**免费打出**（埋的时候已经付过费），等于把 0 费子弹**存起来延后兑现**；
    /// ③ 区域的张数本身是「倾巢」「倾泄」「决堤」「猛狐连击」的燃料。
    /// </para>
    /// <para>
    /// **沿革**：①2026-09-25 用户砍掉"挂冰"段（填满+挂冰超模），升级改为"填进去的是雪球+"（质变型）；
    /// ②**2026-09-28 用户定稿**：自带消耗、升级去消耗（反复填区是升级位），填的**永远是普通雪球**
    /// （雪球+渠道还给「打雪仗」「雪球保护」的升级）。
    /// </para>
    /// <para>
    /// **与「雪灾」互为镜像**：雪灾是**负向填充**——塞进不可打出的晕眩，自锁换爆发；
    /// 本卡是**正向填充**——塞进有价值、会被免费打出的雪球，用"占格"换力量平台与延后弹药。
    /// 分工上「雪球保护」「打雪仗」把弹送**手牌**（立刻可用），本卡送**区域**（延后 + 换力量 + 喂张数卡）。
    /// </para>
    /// </summary>
    public sealed class SnowballTechnique : CardModel, ILingdawnpaoCard
    {
        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Snowball>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        public SnowballTechnique()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

            // 填到上限：循环条件先判满，避免多造一张孤儿牌（照「雪灾」填晕眩的写法）
            await AmbushZone.BringOutBeforeGenerateAsync(choiceContext, cardPlay.Target, Owner);
            AmbushZoneState zone = AmbushZone.For(Owner);
            while (!zone.IsFull && !zone.IsCoolingDown)
            {
                Snowball snowball = CombatState!.CreateCard<Snowball>(Owner);
                if (!await AmbushZone.BuryGeneratedAsync(choiceContext, Owner, snowball, "雪球技巧"))
                {
                    break;
                }
            }
        }

        protected override void OnUpgrade()
        {
            // 质变升级：去掉【消耗】（升级位 = 反复填区）；填进去的永远是普通雪球
            RemoveKeyword(CardKeyword.Exhaust);
        }
    }
}
