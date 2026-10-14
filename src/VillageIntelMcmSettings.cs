using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Localization;

namespace VillageIntel
{
    /// <summary>The "Village Intel" page in Mod Options (MCM). Saved to Documents\Mount and Blade II Bannerlord\Configs\ModSettings.</summary>
    public sealed class VillageIntelMcmSettings : AttributeGlobalSettings<VillageIntelMcmSettings>
    {
        public override string Id => "VillageIntel";
        // MCM doesn't translate DisplayName itself, so resolve the {=id} here.
        public override string DisplayName => new TextObject("{=vi_mcm_title}Village Intel").ToString();
        public override string FolderName => "VillageIntel";
        public override string FormatType => "json2";

        // ---------------- General ----------------
        private const string General = "{=vi_mcm_group_general}General";

        [SettingPropertyBool("{=vi_mcm_Enabled}Enable", Order = 0, RequireRestart = false, HintText = "{=vi_mcm_Enabled_hint}Turn the 'ask about the area' dialogue on or off.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool("{=vi_mcm_AllowTownNotables}Town notables too", Order = 1, RequireRestart = false,
            HintText = "{=vi_mcm_AllowTownNotables_hint}Off: only village notables (headmen and rural notables) can be asked. On: town notables (merchants, artisans, gang leaders) can be asked as well.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool AllowTownNotables { get; set; } = false;

        [SettingPropertyFloatingInteger("{=vi_mcm_SearchRadius}Search radius", 10f, 150f, "0", Order = 2, RequireRestart = false,
            HintText = "{=vi_mcm_SearchRadius_hint}How far from the village (in campaign map units) a notable can have heard of parties and hideouts. Roughly 5 units is an hour's ride.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public float SearchRadius { get; set; } = 40f;

        [SettingPropertyInteger("{=vi_mcm_MaxReports}Most sightings per answer", 1, 8, "0", Order = 3, RequireRestart = false,
            HintText = "{=vi_mcm_MaxReports_hint}The most parties or hideouts a notable will describe in one answer, nearest first.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public int MaxReports { get; set; } = 3;

        [SettingPropertyFloatingInteger("{=vi_mcm_CooldownHours}Hours before asking again", 0f, 168f, "0", Order = 4, RequireRestart = false,
            HintText = "{=vi_mcm_CooldownHours_hint}Within this many hours, asking the same notable the same question repeats their earlier answer for free instead of giving a new one.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public float CooldownHours { get; set; } = 24f;

        [SettingPropertyBool("{=vi_mcm_RevealHideouts}Truthful hideout reports mark the map", Order = 5, RequireRestart = false,
            HintText = "{=vi_mcm_RevealHideouts_hint}When a notable tells the truth about a hideout, it is marked on the campaign map. Handy, but it also tells you when a notable lied.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool RevealHideouts { get; set; } = false;

        // ---------------- Knowledge ----------------
        private const string Knowledge = "{=vi_mcm_group_knowledge}Knowledge";

        [SettingPropertyFloatingInteger("{=vi_mcm_KnowChanceNear}Chance to know: on the doorstep", 0f, 1f, "0%", Order = 0, RequireRestart = false,
            HintText = "{=vi_mcm_KnowChanceNear_hint}Chance a notable has heard of a party right next to the village. It falls off evenly to the far value at the edge of the search radius.")]
        [SettingPropertyGroup(Knowledge, GroupOrder = 1)]
        public float KnowChanceNear { get; set; } = 0.95f;

        [SettingPropertyFloatingInteger("{=vi_mcm_KnowChanceFar}Chance to know: edge of radius", 0f, 1f, "0%", Order = 1, RequireRestart = false,
            HintText = "{=vi_mcm_KnowChanceFar_hint}Chance a notable has heard of a party at the edge of the search radius.")]
        [SettingPropertyGroup(Knowledge, GroupOrder = 1)]
        public float KnowChanceFar { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("{=vi_mcm_HideoutKnowBonus}Bonus for hideouts", 0f, 1f, "0%", Order = 2, RequireRestart = false,
            HintText = "{=vi_mcm_HideoutKnowBonus_hint}Hideouts don't move, so locals are more likely to know where they are.")]
        [SettingPropertyGroup(Knowledge, GroupOrder = 1)]
        public float HideoutKnowBonus { get; set; } = 0.15f;

        [SettingPropertyFloatingInteger("{=vi_mcm_ArmyKnowBonus}Bonus for armies", 0f, 1f, "0%", Order = 3, RequireRestart = false,
            HintText = "{=vi_mcm_ArmyKnowBonus_hint}Armies are hard to miss.")]
        [SettingPropertyGroup(Knowledge, GroupOrder = 1)]
        public float ArmyKnowBonus { get; set; } = 0.2f;

        // ---------------- Honesty ----------------
        private const string Honesty = "{=vi_mcm_group_honesty}Honesty";

        [SettingPropertyFloatingInteger("{=vi_mcm_BaseLieChance}Base chance to lie", 0f, 1f, "0%", Order = 0, RequireRestart = false,
            HintText = "{=vi_mcm_BaseLieChance_hint}Chance to lie about a sighting when the notable likes you exactly as much as they like the party in question.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public float BaseLieChance { get; set; } = 0.15f;

        [SettingPropertyFloatingInteger("{=vi_mcm_LieChancePerPoint}Lie chance per relation point", 0f, 0.02f, "0.000", Order = 1, RequireRestart = false,
            HintText = "{=vi_mcm_LieChancePerPoint_hint}Trust = (notable's relation with you) - (notable's relation with the party). Each point of trust lowers the lie chance by this much, and each point of distrust raises it.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public float LieChancePerPoint { get; set; } = 0.005f;

        [SettingPropertyFloatingInteger("{=vi_mcm_MaxLieChance}Maximum chance to lie", 0f, 1f, "0%", Order = 2, RequireRestart = false,
            HintText = "{=vi_mcm_MaxLieChance_hint}No notable lies more often than this, however much they dislike you or like the party in question.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public float MaxLieChance { get; set; } = 0.9f;

        [SettingPropertyFloatingInteger("{=vi_mcm_LiePerDishonorLevel}Dishonesty per Honor level", 0f, 0.5f, "0%", Order = 3, RequireRestart = false,
            HintText = "{=vi_mcm_LiePerDishonorLevel_hint}Each level of negative Honor adds this to the notable's lie chance; each positive level takes half of it away.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public float LiePerDishonorLevel { get; set; } = 0.1f;

        [SettingPropertyInteger("{=vi_mcm_OwnRealmLoyalty}Loyalty to own realm", 0, 100, "0", Order = 4, RequireRestart = false,
            HintText = "{=vi_mcm_OwnRealmLoyalty_hint}Added to a notable's relation with a party that belongs to the village's own realm (they protect their own lords).")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public int OwnRealmLoyalty { get; set; } = 30;

        [SettingPropertyInteger("{=vi_mcm_EnemyRealmHatred}Hatred of the village's enemies", 0, 100, "0", Order = 5, RequireRestart = false,
            HintText = "{=vi_mcm_EnemyRealmHatred_hint}Subtracted from a notable's relation with a party whose realm is at war with the village's realm.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public int EnemyRealmHatred { get; set; } = 20;

        [SettingPropertyInteger("{=vi_mcm_BanditBaseRelation}Relation with bandits", -100, 100, "0", Order = 6, RequireRestart = false,
            HintText = "{=vi_mcm_BanditBaseRelation_hint}A notable's relation with bandits before Honor is applied. Villagers usually hate them.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public int BanditBaseRelation { get; set; } = -20;

        [SettingPropertyInteger("{=vi_mcm_BanditRelationPerHonor}Bandit sympathy per Honor level", 0, 50, "0", Order = 7, RequireRestart = false,
            HintText = "{=vi_mcm_BanditRelationPerHonor_hint}Each level of negative Honor makes the notable this much friendlier with bandits (they may be in league with them); each positive level makes them this much more hostile.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public int BanditRelationPerHonor { get; set; } = 25;

        [SettingPropertyInteger("{=vi_mcm_NoticeSkillLead}Skill lead to notice a lie", 0, 300, "0", Order = 8, RequireRestart = false,
            HintText = "{=vi_mcm_NoticeSkillLead_hint}You notice a lie when your Charm, Scouting or Roguery is higher than the notable's by more than this. 0 means any higher skill is enough.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public int NoticeSkillLead { get; set; } = 0;

        [SettingPropertyFloatingInteger("{=vi_mcm_PressBaseChance}Pressing: base chance", 0f, 1f, "0%", Order = 9, RequireRestart = false,
            HintText = "{=vi_mcm_PressBaseChance_hint}Chance that calling out a lie makes the notable tell the truth, before your skill lead and relation are added.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public float PressBaseChance { get; set; } = 0.4f;

        [SettingPropertyFloatingInteger("{=vi_mcm_PressChancePerPoint}Pressing: chance per skill or relation point", 0f, 0.02f, "0.000", Order = 10, RequireRestart = false,
            HintText = "{=vi_mcm_PressChancePerPoint_hint}Each point by which your best skill (Charm, Scouting or Roguery) beats the notable's, and each point of relation, adds this to the pressing chance. Bad relation takes it away.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public float PressChancePerPoint { get; set; } = 0.004f;

        [SettingPropertyInteger("{=vi_mcm_PressFailRelation}Pressing: relation lost on failure", 0, 30, "0", Order = 11, RequireRestart = false,
            HintText = "{=vi_mcm_PressFailRelation_hint}How much relation you lose when you call a notable a liar and they won't back down.")]
        [SettingPropertyGroup(Honesty, GroupOrder = 2)]
        public int PressFailRelation { get; set; } = 3;

        // ---------------- Price ----------------
        private const string Price = "{=vi_mcm_group_price}Price";

        [SettingPropertyInteger("{=vi_mcm_RefuseRelation}Relation to talk at all", -100, 100, "0", Order = 0, RequireRestart = false,
            HintText = "{=vi_mcm_RefuseRelation_hint}Notables whose relation with you is at or below this refuse to tell you anything.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public int RefuseRelation { get; set; } = -30;

        [SettingPropertyInteger("{=vi_mcm_FreeRelation}Relation for free information", -100, 100, "0", Order = 1, RequireRestart = false,
            HintText = "{=vi_mcm_FreeRelation_hint}Notables whose relation with you is at or above this tell you for free.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public int FreeRelation { get; set; } = 30;

        [SettingPropertyInteger("{=vi_mcm_EnemyBasePrice}Price: enemy parties", 0, 5000, "0", Order = 2, RequireRestart = false,
            HintText = "{=vi_mcm_EnemyBasePrice_hint}Base asking price in denars for news of enemy war parties, at 0 relation.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public int EnemyBasePrice { get; set; } = 100;

        [SettingPropertyInteger("{=vi_mcm_BanditBasePrice}Price: bandit gangs", 0, 5000, "0", Order = 3, RequireRestart = false,
            HintText = "{=vi_mcm_BanditBasePrice_hint}Base asking price in denars for news of roaming bandit gangs, at 0 relation.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public int BanditBasePrice { get; set; } = 60;

        [SettingPropertyInteger("{=vi_mcm_HideoutBasePrice}Price: bandit hideouts", 0, 5000, "0", Order = 3, RequireRestart = false,
            HintText = "{=vi_mcm_HideoutBasePrice_hint}Base asking price in denars for the whereabouts of bandit hideouts, at 0 relation.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public int HideoutBasePrice { get; set; } = 120;

        [SettingPropertyFloatingInteger("{=vi_mcm_PricePerBadRelation}Extra price per bad relation point", 0f, 0.1f, "0.000", Order = 4, RequireRestart = false,
            HintText = "{=vi_mcm_PricePerBadRelation_hint}Each point of relation below 0 raises the price by this fraction. Each point above 0 lowers it by half as much.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public float PricePerBadRelation { get; set; } = 0.03f;

        [SettingPropertyFloatingInteger("{=vi_mcm_MaxCharmDiscount}Charm discount at 300", 0f, 1f, "0%", Order = 5, RequireRestart = false,
            HintText = "{=vi_mcm_MaxCharmDiscount_hint}Discount you get with 300 Charm. Scales linearly with your Charm skill.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public float MaxCharmDiscount { get; set; } = 0.4f;

        [SettingPropertyFloatingInteger("{=vi_mcm_GoodsChance}Chance goods are accepted", 0f, 1f, "0%", Order = 6, RequireRestart = false,
            HintText = "{=vi_mcm_GoodsChance_hint}Chance the notable will take food or trade goods from your inventory as payment, if you have enough.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public float GoodsChance { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("{=vi_mcm_GoodsOnlyChance}Chance only goods will do", 0f, 1f, "0%", Order = 7, RequireRestart = false,
            HintText = "{=vi_mcm_GoodsOnlyChance_hint}When goods are accepted, the chance the notable refuses coin and wants the goods instead.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public float GoodsOnlyChance { get; set; } = 0.4f;

        [SettingPropertyFloatingInteger("{=vi_mcm_GoodsMarkup}Goods markup", 0.5f, 3f, "0.00x", Order = 8, RequireRestart = false,
            HintText = "{=vi_mcm_GoodsMarkup_hint}How much base value of goods the notable wants for each denar of the price.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public float GoodsMarkup { get; set; } = 1.2f;

        [SettingPropertyInteger("{=vi_mcm_RelationOnPay}Relation gained when paying", 0, 10, "0", Order = 9, RequireRestart = false,
            HintText = "{=vi_mcm_RelationOnPay_hint}Relation you gain with a notable each time you pay them in gold or goods.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public int RelationOnPay { get; set; } = 1;

        [SettingPropertyBool("{=vi_mcm_AllowPersuasion}Allow persuasion", Order = 10, RequireRestart = false,
            HintText = "{=vi_mcm_AllowPersuasion_hint}When a notable asks to be paid, you can instead try to talk them round with the game's normal persuasion system (one try per question). Success gets the answer for free and gives skill experience like other persuasions.")]
        [SettingPropertyGroup(Price, GroupOrder = 3)]
        public bool AllowPersuasion { get; set; } = true;
    }
}
