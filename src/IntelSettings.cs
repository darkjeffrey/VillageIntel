using System;

namespace VillageIntel
{
    /// <summary>
    /// Read-only view of the current settings. Values come from the MCM page
    /// (VillageIntelMcmSettings); the defaults there are used if MCM isn't loaded yet.
    /// </summary>
    public static class IntelSettings
    {
        private static readonly VillageIntelMcmSettings Defaults = new VillageIntelMcmSettings();

        private static VillageIntelMcmSettings S
        {
            get
            {
                try { return VillageIntelMcmSettings.Instance ?? Defaults; }
                catch (Exception) { return Defaults; }
            }
        }

        private static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));

        public static bool Enabled => S.Enabled;
        public static bool AllowTownNotables => S.AllowTownNotables;
        public static float SearchRadius => Math.Max(1f, S.SearchRadius);
        public static int MaxReports => Math.Max(1, S.MaxReports);
        public static float CooldownHours => Math.Max(0f, S.CooldownHours);
        public static bool RevealHideouts => S.RevealHideouts;

        public static float KnowChanceNear => Clamp01(S.KnowChanceNear);
        public static float KnowChanceFar => Clamp01(S.KnowChanceFar);
        public static float HideoutKnowBonus => Math.Max(0f, S.HideoutKnowBonus);
        public static float ArmyKnowBonus => Math.Max(0f, S.ArmyKnowBonus);

        public static float BaseLieChance => Clamp01(S.BaseLieChance);
        public static float LieChancePerPoint => Math.Max(0f, S.LieChancePerPoint);
        public static float MaxLieChance => Clamp01(S.MaxLieChance);
        public static float LiePerDishonorLevel => Math.Max(0f, S.LiePerDishonorLevel);
        public static int OwnRealmLoyalty => S.OwnRealmLoyalty;
        public static int EnemyRealmHatred => S.EnemyRealmHatred;
        public static int BanditBaseRelation => S.BanditBaseRelation;
        public static int BanditRelationPerHonor => S.BanditRelationPerHonor;
        public static int NoticeSkillLead => Math.Max(0, S.NoticeSkillLead);
        public static float PressBaseChance => Clamp01(S.PressBaseChance);
        public static float PressChancePerPoint => Math.Max(0f, S.PressChancePerPoint);
        public static int PressFailRelation => Math.Max(0, S.PressFailRelation);

        public static int RefuseRelation => S.RefuseRelation;
        public static int FreeRelation => S.FreeRelation;
        public static int EnemyBasePrice => Math.Max(0, S.EnemyBasePrice);
        public static int BanditBasePrice => Math.Max(0, S.BanditBasePrice);
        public static int HideoutBasePrice => Math.Max(0, S.HideoutBasePrice);
        public static bool AllowPersuasion => S.AllowPersuasion;
        public static float PricePerBadRelation => Math.Max(0f, S.PricePerBadRelation);
        public static float MaxCharmDiscount => Clamp01(S.MaxCharmDiscount);
        public static float GoodsChance => Clamp01(S.GoodsChance);
        public static float GoodsOnlyChance => Clamp01(S.GoodsOnlyChance);
        public static float GoodsMarkup => Math.Max(0.1f, S.GoodsMarkup);
        public static int RelationOnPay => Math.Max(0, S.RelationOnPay);

        // ---------------- Debug (Debug group in the mod menu) ----------------
        public static bool DebugMode => S.DebugMode;
        public static bool DebugAlwaysKnow => S.DebugMode && S.DebugAlwaysKnow;
        public static bool DebugNoCooldown => S.DebugMode && S.DebugNoCooldown;
    }
}
