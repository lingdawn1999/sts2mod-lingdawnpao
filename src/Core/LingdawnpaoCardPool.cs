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
                ModelDb.Card<FrostCoat>(),
                ModelDb.Card<FrostStrike>(),
                ModelDb.Card<LeopardBite>(),
                ModelDb.Card<SetAmbush>(),
                ModelDb.Card<Ingenuity>(),
                ModelDb.Card<WarmSnow>(),
                ModelDb.Card<Expansion>(),
                ModelDb.Card<Overload>(),
                ModelDb.Card<FrozenAdeptCard>(),
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
                ModelDb.Card<Snowpack>(),
                ModelDb.Card<ChainPounce>(),
                ModelDb.Card<EternalHunt>(),
                ModelDb.Card<NightPatrol>(),
                ModelDb.Card<Hunt>(),
                ModelDb.Card<EternalBreath>(),
                ModelDb.Card<FrostThrone>(),
                ModelDb.Card<FrostBreath>(),
                ModelDb.Card<FrostField>(),
                ModelDb.Card<DeepWinter>(),
                ModelDb.Card<Avalanche>(),
                ModelDb.Card<IceArmor>(),
                ModelDb.Card<IceAge>(),
                ModelDb.Card<FrostClaw>(),
                ModelDb.Card<FrostDoubling>(),
                ModelDb.Card<FrostCalamity>(),
                ModelDb.Card<SnowballTechnique>(),
                ModelDb.Card<SnowballGuard>(),
                ModelDb.Card<SnowballFight>(),
                ModelDb.Card<CarnivalMoment>(),
                ModelDb.Card<BigSnowballVolley>(),
                ModelDb.Card<Snowfield>(),
                ModelDb.Card<WinterMelt>(),
                ModelDb.Card<FrostIntensify>(),
                ModelDb.Card<SnowballLauncher>(),
                ModelDb.Card<IceShatter>(),
                ModelDb.Card<IceBody>(),
                ModelDb.Card<Blizzard>(),
                ModelDb.Card<SnowCalamity>(),
                ModelDb.Card<SpinosaurusForm>(),
                ModelDb.Card<SnowSift>(),
                ModelDb.Card<SnowfieldDash>(),
                ModelDb.Card<SnowPelt>(),
                ModelDb.Card<ThickCoat>(),
                ModelDb.Card<ProbingClaw>(),
                ModelDb.Card<EarthshakerPalm>(),
                ModelDb.Card<Hiss>(),
                ModelDb.Card<BodyPounce>(),
                ModelDb.Card<ClawAssault>(),
                ModelDb.Card<SwarmAssault>(),
                ModelDb.Card<ChainClaw>(),
                ModelDb.Card<SnowFlurry>(),
                ModelDb.Card<Icebreaker>(),
                ModelDb.Card<SnowPeakCrush>(),
                ModelDb.Card<SnowstormTorrent>(),
                ModelDb.Card<Frostbloom>(),
                ModelDb.Card<TingQiao>(),
                ModelDb.Card<HoldFast>(),
                ModelDb.Card<Trove>(),
                ModelDb.Card<IceMirror>(),
                ModelDb.Card<FiercePounce>(),
                ModelDb.Card<Concentration>(),
                ModelDb.Card<Devour>(),
                ModelDb.Card<AnimalFriends>(),
                ModelDb.Card<Stockpile>(),
                ModelDb.Card<Snowdrift>(),
                ModelDb.Card<AnimalFamily>(),
                ModelDb.Card<SwarmAmbush>(),
                ModelDb.Card<LungItch>(),
                ModelDb.Card<SharePrey>(),
                ModelDb.Card<PetTheFluffy>(),
                ModelDb.Card<WildDogMilk>(),
            };
        }
    }
}
