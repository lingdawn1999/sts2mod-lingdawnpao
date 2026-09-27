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
    /// 雪球技巧：**1 费罕见（蓝）技能**。**用「雪球」填满你的[gold]伏击区域[/gold]**（填到当前上限）。
    /// **升级是质变型**：填进去的雪球全部为升级版「雪球+」（挂冰 2 → 3，照「设伏」「捕猎」「永恒呼吸」「打雪仗」的同族写法）。
    /// 定位：**雪球线 → 伏击线的"输送带"**（两条机制之间的润滑件）。
    /// <para>
    /// **为什么埋区域比塞手牌值**（这是本卡区别于「雪球保护」「打雪仗」的卖点）：
    /// ① 伏击区按牌**类型**给加成——雪球是[gold]攻击牌[/gold]，在区域里每张给 **+1 [gold]力量[/gold]**，
    ///    填满 3 格就是一座稳定的力量平台（「扩张」「暗杀者」把上限抬到 4 时更高）；
    /// ② 带出时是**免费打出**（埋的时候已经付过费），等于把 0 费子弹**存起来延后兑现**；
    /// ③ 区域的张数本身是「倾巢」「倾泄」「决堤」「猛狐连击」的燃料。
    /// </para>
    /// <para>
    /// **2026-09-25 用户砍掉"挂冰"那一段**（原效果：给予 3 层[gold]冰冻[/gold]（升级 5）**+** 填满区域），
    /// 理由是"填满区域 + 挂冰"两条收益叠在一起**超模**——去掉挂冰后才合模。连带三处变化：
    /// ① **目标类型 AnyEnemy → Self**（不再需要选敌人）；
    /// ② 数值变量清空（本卡现在没有任何数值，升级改走质变型）；
    /// ③ 升级位从"冰冻 3 → 5"改成"填进去的雪球是升级版"——否则这张牌会完全没有升级收益。
    /// 保留的"填满区域"本身就是实打实的收益（力量平台 + 3~5 张免费弹药），所以它仍然立得住。
    /// </para>
    /// <para>
    /// **与「雪灾」互为镜像**：雪灾是**负向填充**——清空区域后塞进不可打出的晕眩，自锁换爆发；
    /// 本卡是**正向填充**——塞进有价值、会被免费打出的雪球，用"占格"换力量平台与延后弹药。
    /// 分工上「雪球保护」「打雪仗」把弹送**手牌**（立刻可用），本卡送**区域**（延后 + 换力量 + 喂张数卡）。
    /// </para>
    /// <para>
    /// **结算顺序（v2）**：直接填雪球——v2 普通出牌不再自动带出，埋进去就老实待着
    /// （区域冷却分支随 v2 语义一并移除：失效只抑制力敏，入区照常）。
    /// </para>
    /// </summary>
    public sealed class SnowballTechnique : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Snowball>(upgrade: IsUpgraded),
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
                if (IsUpgraded)
                {
                    CardCmd.Upgrade(snowball);
                }

                if (!await AmbushZone.BuryGeneratedAsync(choiceContext, Owner, snowball, "雪球技巧"))
                {
                    break;
                }
            }
        }

        protected override void OnUpgrade()
        {
            // 质变升级：张数由"填满"决定，升级只改"填进去的是不是雪球+"（挂冰 2 → 3）
        }
    }
}
