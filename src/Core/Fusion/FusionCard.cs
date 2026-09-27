using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 合体卡（v2 吞噬合成轴的产物，设计稿第三节）：
    /// 「吞噬」选择伏击区域中两张同类型牌合成——两材料移出战斗，产物加入区域顶部。
    /// 产物 = 卡组天花板外的新牌，费用/伤害相加、特效拼接、词条收窄，**不可再升级**
    /// （MaxUpgradeLevel 基类默认 1，天然满足）。
    /// <para>
    /// **按实例动态化的实现方式**（重要，改这卡前先读）：
    /// ① 本体 <c>CreateCard&lt;T&gt;</c> 从 canonical 克隆（<c>ToMutable</c>），构造函数无法带参——
    ///    所以用"无参构造 + <see cref="Configure" /> 注入数据"模式，克隆后立刻调用；
    /// ② <c>Type/TargetType/Rarity</c> 是 virtual get-only——按实例覆写 Type/TargetType，
    ///    一个类同时服务攻击合体与技能合体；
    /// ③ 费用：覆写 <c>CanonicalEnergyCost</c>（virtual），并在 Configure 里用
    ///    <c>MockSetEnergyCost</c> 强刷已缓存的 EnergyCost（克隆链可能提前摸过它）；
    /// ④ 卡图与卡名继承基底材料（玩家在合成时指定的第一张）——覆写 PortraitPath/Title，
    ///    基底是合体卡时自然递归到最初的真实卡；
    /// ⑤ 卡面文本：Description 是非虚属性（固定走 <c>FUSION.description</c> 本地化键），
    ///    模板写在 cards.json，按实例的差异全部经 <see cref="AddExtraArgsToDescription" />
    ///    以变量注入（攻击合体只有 {Damage} 一处活值随力量实时刷新；技能合体正文在
    ///    合成时刻整段烘焙——格挡等数值不吃力量，烘焙即所见即所得，特效代码路径仍活值）。
    /// </para>
    /// <para>
    /// **结算模型**（设计稿"伤害可乘、特效不乘"）：
    /// · 攻+攻：每段伤害 = 两材料每段之和（DamageCmd 单命令 WithHitCount，段数取大），
    ///   附加特效在伤害段之后**各执行一次**（<see cref="IFusionMaterial.PlayFusionEffectAsync" />）；
    /// · 技+技：逐张免费重放材料（<see cref="CardCmd.AutoPlay" />，与兑现带出同一管线），
    ///   同轴数值自然相加（格挡+格挡）、异轴自然拼接；重放后把材料静默移出战斗
    ///   （重放会让它落弃牌堆——"材料移出战斗"的约定不允许它第三次进场）。
    /// </para>
    /// <para>
    /// **联机**：Configure 与 OnPlay 都跑在同步动作队列里（打牌/兑现动作内部，全端同序执行），
    /// 合体卡本身经 CreateCard 进战斗状态，两端的材料引用、烘焙文本一致。
    /// </para>
    /// <para>
    /// ⚠️ 卡面两段封顶：攻击合体 = 伤害段 + 特效段；技能合体 = 材料正文拼接
    /// （可能超过两段——材料卡面本身多行时，字号溢出风险已知，待整体卡面自适应统一处理）。
    /// 材料的保留/消耗/虚无词条按收窄规则继承到产物（<see cref="Configure" /> 里 AddKeyword），
    /// 材料正文中自带的词条行会随之重复显示一次——已知冗余，待本地化全量重写时统一收口。
    /// </para>
    /// </summary>
    public sealed class FusionCard : CardModel
    {
        private CardModel _baseMaterial = null!;

        private CardModel _fuelMaterial = null!;

        private CardType _type = CardType.Skill;

        private TargetType _targetType = TargetType.None;

        private int _cost;

        private decimal _displayPerSegment;

        private int _displaySegments = 1;

        private string _effects = string.Empty;

        private string _body = string.Empty;

        /// <summary>TokenCardPool 登记（ModelDb 需要），不进卡池不进图鉴、不进奖励。</summary>
        public FusionCard()
            : base(1, CardType.Skill, CardRarity.Token, TargetType.None, shouldShowInCardLibrary: false)
        {
        }

        /// <summary>基底材料（卡名/卡图显示来源，玩家合成时指定的第一张）。</summary>
        public CardModel BaseMaterial => _baseMaterial;

        /// <summary>燃料材料（只贡献效果段）。</summary>
        public CardModel FuelMaterial => _fuelMaterial;

        /// <summary>
        /// 每段基础伤害的**单一字段**（设计稿"单计算链"：磨爪/十万埋伏/倾巢等联动卡只读这个，
        /// 联动卡之间不叠加口径）。攻击合体才有意义；合成时刻烘焙，材料此后冻结、数值一致。
        /// </summary>
        public decimal DisplayPerSegmentDamage => _displayPerSegment;

        /// <summary>合体卡段数（两材料段数的较大者）。</summary>
        public int DisplaySegments => _displaySegments;

        public override CardType Type => _type;

        public override TargetType TargetType => _targetType;

        /// <summary>卡名继承基底（含其升级后缀）。空引用保护：Configure 前被读到时回落到 FUSION.title。</summary>
        public override string Title => _baseMaterial?.Title
            ?? new LocString("cards", "FUSION.title").GetFormattedText();

        public override string PortraitPath => _baseMaterial?.PortraitPath ?? base.PortraitPath;

        public override string BetaPortraitPath => _baseMaterial?.BetaPortraitPath ?? base.BetaPortraitPath;

        protected override int CanonicalEnergyCost => _cost;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
            _type == CardType.Attack
                ? new DynamicVar[] { new DamageVar(_displayPerSegment, ValueProp.Move) }
                : Array.Empty<DynamicVar>();

        /// <summary>
        /// 克隆后立刻调用：注入两材料并推导全部合成数据（设计稿第三节规则）。
        /// <paramref name="baseMaterial" /> 为基底（卡名卡图来源），<paramref name="fuelMaterial" /> 为燃料。
        /// </summary>
        public void Configure(CardModel baseMaterial, CardModel fuelMaterial)
        {
            _baseMaterial = baseMaterial;
            _fuelMaterial = fuelMaterial;
            _type = baseMaterial.Type;
            // 目标类型：攻击合体单体指向；技能合体沿用基底的（重放时目标会传给材料 OnPlay）
            _targetType = _type == CardType.Attack ? TargetType.AnyEnemy : baseMaterial.TargetType;

            // 费用 = 相加（X 费材料已在 FusionEngine.IsFusable 拒掉，这里不会再见到 X）
            _cost = baseMaterial.EnergyCost.Canonical + fuelMaterial.EnergyCost.Canonical;
            // 克隆链可能已缓存过 EnergyCost（canonical 上的 1 费）——强刷成真实合成费用
            MockSetEnergyCost(new CardEnergyCost(this, _cost, false));

            // 词条收窄：保留（宽）/ 消耗、虚无（严）——任一材料带则产物带；埋伏不继承（见类注释）
            foreach (CardKeyword keyword in new[] { CardKeyword.Retain, CardKeyword.Exhaust, CardKeyword.Ethereal })
            {
                if (baseMaterial.Keywords.Contains(keyword) || fuelMaterial.Keywords.Contains(keyword))
                {
                    AddKeyword(keyword);
                }
            }

            // 显示轴：合成时刻烘焙（材料此后冻结，且材料数值本来就不吃力敏——除攻击伤害外）
            _displayPerSegment = FusionEngine.DamagePerSegment(baseMaterial) + FusionEngine.DamagePerSegment(fuelMaterial);
            _displaySegments = Math.Max(FusionEngine.SegmentsOf(baseMaterial), FusionEngine.SegmentsOf(fuelMaterial));

            if (_type == CardType.Attack)
            {
                // 攻击合体：附加段 = 各攻击材料的非伤害特效文本（各执行一次，永不随段数乘）
                List<string> lines = new List<string>();
                foreach (CardModel mat in new[] { baseMaterial, fuelMaterial })
                {
                    if (mat is IFusionMaterial part && !string.IsNullOrEmpty(part.FusionEffectLine))
                    {
                        lines.Add(part.FusionEffectLine);
                    }
                }

                _effects = lines.Count > 0 ? "\n" + string.Join("\n", lines) : string.Empty;
            }
            else
            {
                // 技能合体：正文 = 两材料卡面拼接（GetDescriptionForPile 带动态变量求值）。
                // 材料正文里的词条行会与产物自身词条行重复——已知冗余，见类注释。
                _body = string.Join("\n", new[] { baseMaterial, fuelMaterial }
                    .Select(mat => mat.GetDescriptionForPile(PileType.None)));
            }
        }

        /// <summary>卡面模板变量注入（模板见 cards.json 的 FUSION.description）。</summary>
        protected override void AddExtraArgsToDescription(LocString description)
        {
            description.Add("IsAttack", _type == CardType.Attack);
            description.Add("Segments", (decimal)_displaySegments);
            description.Add("Effects", _effects);
            description.Add("Body", _body);
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            if (_type == CardType.Attack)
            {
                ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

                // 伤害轴相加、段数取大（活值：读材料当前 DynamicVars，力量经 FromCardCompat 施加在合体卡上）
                decimal perSegment = FusionEngine.DamagePerSegment(_baseMaterial) + FusionEngine.DamagePerSegment(_fuelMaterial);
                int hits = Math.Max(FusionEngine.SegmentsOf(_baseMaterial), FusionEngine.SegmentsOf(_fuelMaterial));
                await DamageCmd.Attack(perSegment).WithHitCount(hits).FromCardCompat(this, cardPlay)
                    .Targeting(cardPlay.Target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);

                // 特效轴拼接：各材料的附加特效在伤害段结算完后各执行一次，永不随段数乘
                foreach (IFusionMaterial part in FusionEngine.FusionParts(_baseMaterial).Concat(FusionEngine.FusionParts(_fuelMaterial)))
                {
                    await part.PlayFusionEffectAsync(choiceContext, cardPlay);
                }

                return;
            }

            // 技能合体：逐张免费重放（同轴相加、异轴拼接），重放后把材料静默移出战斗
            foreach (CardModel mat in new[] { _baseMaterial, _fuelMaterial })
            {
                await CardCmd.AutoPlay(choiceContext, mat, cardPlay.Target, skipCardPileVisuals: true);
                await FusionEngine.RemoveReplayedMaterialAsync(choiceContext, mat, Owner);
            }
        }
    }
}
