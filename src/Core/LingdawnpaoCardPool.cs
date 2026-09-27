using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 芝士雪豹卡池，全部为自有卡。卡框颜色由 CardModel_FrameMaterial 补丁染浅冰蓝；
    /// CardFrameMaterialPath 保留 card_frame_blue 用于牌组查看界面的背景色调。
    /// </summary>
    public sealed class LingdawnpaoCardPool : CardPoolModel
    {
        public override string Title => "lingdawnpao";

        public override string EnergyColorName => "defect";

        public override string CardFrameMaterialPath => "card_frame_blue";

        public override Color DeckEntryCardColor => new Color("3EB3ED");

        public override Color EnergyOutlineColor => new Color("1D5673");

        public override bool IsColorless => false;

        protected override CardModel[] GenerateAllCards()
        {
            // 全部为芝士雪豹自有卡。借用铁甲卡的占位方案已移除（回归各自本体卡池）。
            return new CardModel[]
            {
                ModelDb.Card<SneakAttack>(),
                ModelDb.Card<HuntOperation>(),
                ModelDb.Card<DamBreak>(),
                ModelDb.Card<TigerCombo>(),
                ModelDb.Card<WidenedNet>(),
                ModelDb.Card<ClawSwipe>(),
                ModelDb.Card<FurGuard>(),
                ModelDb.Card<GatherMomentum>(),
                // 【v2 退役归档】覆霜（FrostCoat）已下架——冰冻体系整体退役，名额给猎手布局
                ModelDb.Card<FrostStrike>(),
                ModelDb.Card<LeopardBite>(),
                ModelDb.Card<SetAmbush>(),
                ModelDb.Card<Ingenuity>(),
                ModelDb.Card<WarmSnow>(),
                ModelDb.Card<Expansion>(),
                ModelDb.Card<Overload>(),
                // 【v2 退役归档】冰封（FrozenAdeptCard）已下架——冰冻倍增件随体系退役
                ModelDb.Card<AmbushPounce>(),
                ModelDb.Card<ThroatGrip>(),
                ModelDb.Card<HoldBreath>(),
                ModelDb.Card<KillerInstinct>(),
                ModelDb.Card<Regroup>(),
                ModelDb.Card<TrapRecovery>(),
                ModelDb.Card<LastStand>(),
                ModelDb.Card<CloseTheNet>(),
                ModelDb.Card<AmbushEcho>(),
                ModelDb.Card<CalamityForm>(),
                ModelDb.Card<DeepBite>(),
                ModelDb.Card<FrostFangCard>(),
                ModelDb.Card<Track>(),
                ModelDb.Card<AmbushBarrage>(),
                ModelDb.Card<FollowTheTrail>(),
                ModelDb.Card<SnowSurge>(),
                ModelDb.Card<WhetClaw>(),
                ModelDb.Card<ReadyForAnything>(),
                ModelDb.Card<FlipFlop>(),
                ModelDb.Card<ChainPounce>(),
                ModelDb.Card<EternalHunt>(),
                ModelDb.Card<NightPatrol>(),
                ModelDb.Card<Hunt>(),
                // 【v2 退役归档】永恒呼吸（EternalBreath）已下架——灌池器退役（规则 5）
                // 【v2 退役归档】冰封王座（FrostThrone）、深冬前的一批冰冻件下架见下
                ModelDb.Card<FrostBreath>(),
                ModelDb.Card<FrostField>(),
                ModelDb.Card<DeepWinter>(),
                // 【v2 退役归档】雪崩（Avalanche）已下架——名额给狩猎图鉴
                // 【v2 退役归档】雪隐（IceArmor）、霜天（IceAge）已下架——冰冻体系退役
                // 2026-09-21 第二批（冰冻/雪球线）：凝冰、寒灾、雪球技巧、雪球保护、打雪仗、
                // 狂欢时刻、大雪球！、雪场、融冬、凝寒、雪球发射器。
                // 【v2 退役归档】其中凝冰（FrostDoubling）、寒灾（FrostCalamity）、雪球技巧（SnowballTechnique）、
                // 雪球保护（SnowballGuard）、打雪仗（SnowballFight）、狂欢时刻（CarnivalMoment）、
                // 大雪球！（BigSnowballVolley）、融冬（WinterMelt）、雪球发射器（SnowballLauncher）已下架
                // （雪球线整线退役方案 A + 冰冻体系退役）；雪场（Snowfield）保留待 v2 改造为入流引擎。
                ModelDb.Card<FrostClaw>(),
                ModelDb.Card<Snowfield>(),
                ModelDb.Card<FrostIntensify>(),
                // 冰冻系纯数值白卡（2026-09-21）：一攻一防、同一个自伤 4 层冰冻的价码
                // （「棘背龙形态」2026-09-24 已升蓝卡，只是列表位置没挪）
                // 【v2 退役归档】棘背龙形态（SpinosaurusForm）已下架——冰冻出口随体系退役
                ModelDb.Card<IceShatter>(),
                ModelDb.Card<IceBody>(),
                ModelDb.Card<Blizzard>(),
                ModelDb.Card<SnowCalamity>(),
                // 通用白卡（2026-09-21）：偏"运转 + 防御"，用来补普通卡池与防御浓度
                ModelDb.Card<SnowSift>(),
                ModelDb.Card<SnowfieldDash>(),
                ModelDb.Card<SnowPelt>(),
                ModelDb.Card<ThickCoat>(),
                ModelDb.Card<ProbingClaw>(),
                ModelDb.Card<EarthshakerPalm>(),
                ModelDb.Card<Hiss>(),
                // 攻击牌形状补齐（2026-09-21）：格挡兑现、伤害+过牌、按伏击区张数加伤
                ModelDb.Card<BodyPounce>(),
                ModelDb.Card<ClawAssault>(),
                ModelDb.Card<SwarmAssault>(),
                ModelDb.Card<ChainClaw>(),
                // 蓝卡攻击（2026-09-21）：随机目标、冰冻条件加伤、3费重击+挂冰、X费
                ModelDb.Card<SnowFlurry>(),
                ModelDb.Card<Icebreaker>(),
                ModelDb.Card<SnowPeakCrush>(),
                ModelDb.Card<SnowstormTorrent>(),
                // 【v2 退役归档】霜华迸发（Frostbloom）已下架——冰冻放大器随体系退役
                // 听桥（2 费技能，2026-09-23 用户升金）：减伤 60% + 动态反弹，机制复杂度够金卡
                ModelDb.Card<TingQiao>(),
                // 金卡技能（2026-09-24 定稿）：4 费「无实体」防守件，费用随伏击区张数递减（最低 0）
                ModelDb.Card<HoldFast>(),
                // 通用/运转收口（2026-09-21）：抽牌堆检索（珍藏）、手牌复制（冰镜）、高伤白卡攻击（猛扑）。
                // 口径：伏击是"伪通用"平台、冰冻线已到位，缺口在真·通用件，故这三张都不碰冰冻/雪球/伏击。
                // 2026-09-24 定稿：珍藏选中的「埋伏」牌直接埋入伏击区；猛扑 2 费 23 伤并塞 1 张晕眩。
                ModelDb.Card<Trove>(),
                ModelDb.Card<IceMirror>(),
                ModelDb.Card<FiercePounce>(),
                // 凝神（2026-09-21）：手牌技能牌全 0 费，代价是当回合伏击区失效
                ModelDb.Card<Concentration>(),
                // 神速（原「吞噬」，2026-09-23 用户重做）：1 费金卡能力，打出多一个额外回合 + 每回合开始挂 5 层冰冻（升级 3）
                ModelDb.Card<Devour>(),
                // 动物朋友（2026-09-21）：每回合随机发一张「动物」（6 只 0 费消耗 token 不进本池）
                ModelDb.Card<AnimalFriends>(),
                // 收口两张（2026-09-21）：跨回合能量（蓄能·白）＋ 雪拥（1 费金卡：格挡随抽牌堆张数成长，
                // 再把弃牌堆洗回抽牌堆，兼作后期运转件）
                // 【v2 退役归档】雪拥（Snowdrift）已下架——冰冻体系退役
                ModelDb.Card<Stockpile>(),
                // 观星（2026-09-21）：回合开始看牌堆顶 5 张、可弃置任意张（蓝卡能力，升级给固有）
                // 2026-09-22 用户把它移出本池、改挂无色池（商店可购、全角色可用），见 Stargazing 类注释。
                // 联机专属卡（2026-09-22）：MultiplayerOnly，单人不进卡池。
                ModelDb.Card<AnimalFamily>(),
                ModelDb.Card<SwarmAmbush>(),
                ModelDb.Card<LungItch>(),
                ModelDb.Card<SharePrey>(),
                ModelDb.Card<PetTheFluffy>(),
                // 野生狗奶（2026-09-23）：2 费金卡能力，埋伏收益不再需要等待（升级降 1 费）。
                ModelDb.Card<WildDogMilk>(),
                // 吞噬（v2，2026-09-26）：1 费蓝卡技能【消耗】，选区域两张同类型牌合成合体卡
                // （引擎见 Fusion/FusionEngine；产物 FusionCard 挂 TokenCardPool）
                ModelDb.Card<Cannibalize>(),
            };
        }
    }
}
