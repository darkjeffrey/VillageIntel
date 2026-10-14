using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace VillageIntel
{
    /// <summary>The three questions a notable can be asked. Each is priced and remembered separately.</summary>
    public enum IntelTopic
    {
        EnemyParties,
        BanditParties,
        Hideouts
    }

    internal enum Activity
    {
        None,
        HeadingFor,
        Raiding,
        Patrolling,
        Besieging,
        StayingAt
    }

    internal enum SightingKind
    {
        Army,
        LordParty,
        Patrol,
        BanditParty,
        Hideout
    }

    /// <summary>One real party or hideout near the village, plus what the notable decided to say about it.</summary>
    internal sealed class Sighting
    {
        public SightingKind Kind;
        public TextObject Label;       // "Derthert's Party", "Looters", "The Army of Caladog", bandit culture name for hideouts
        public TextObject FactionName; // "Vlandia" (null for bandits)
        public Hideout Hideout;
        public Vec2 Position;
        public float Distance;
        public int Men;
        public Activity Activity;
        public Settlement ActivityTarget;
        public int TargetRelation;     // notable's relation with this party (or with bandits)

        // Rolled when the report is built
        public bool Rolled;
        public float KnowChance;
        public bool Known;
        public float LieChance;
        public bool Lied;
        public bool Concealed;         // lie by omission
        public bool WasLie;            // lied, then confessed after being pressed

        // What the notable says (already localized). The lie, if any, is baked in.
        public string SummaryText;     // "an army of about 300 men"
        public string RefText;         // "the army of about 300 men" (used in the player's follow-up question)
        public string DetailText;      // "That's Caladog's army of Battania. They were seen near Marunath, heading for Seonon."

        public bool IsHideout => Kind == SightingKind.Hideout;
        public bool IsBanditParty => Kind == SightingKind.BanditParty;
    }

    /// <summary>Everything one notable says in answer to one question. Built once, then cached for the cooldown.</summary>
    public sealed class IntelReport
    {
        public IntelTopic Topic;
        public Hero Notable;
        public Settlement Village;
        public CampaignTime CreatedAt;

        internal readonly List<Sighting> Candidates = new List<Sighting>();
        internal readonly List<Sighting> Told = new List<Sighting>();

        public string SummaryText;
        public bool Delivered;         // the summary has been heard at least once (paid, persuaded or free)
        public bool PersuasionTried;   // one persuasion attempt per report
        internal int PlayerRelationAtBuild;

        // Catching the notable out
        public bool LieNoticed;        // the player's Charm, Scouting or Roguery beat the notable's
        public SkillObject NoticeSkill;
        public int NoticeLead;
        public bool Pressed;           // one "you're lying" per report
        public bool Confessed;

        public int LieCount => Candidates.Count(c => c.Lied);

        /// <summary>The notable genuinely knows nothing (no lies of omission either).</summary>
        public bool KnowsNothingHonestly => Candidates.All(c => !c.Known);

        // ------------------------------------------------------------------ building

        public static IntelReport Build(Hero notable, Settlement village, IntelTopic topic)
        {
            var report = new IntelReport
            {
                Topic = topic,
                Notable = notable,
                Village = village,
                CreatedAt = CampaignTime.Now
            };

            Vec2 origin = village.GetPosition2D;
            float radius = IntelSettings.SearchRadius;

            switch (topic)
            {
                case IntelTopic.EnemyParties: GatherEnemyParties(report, origin, radius); break;
                case IntelTopic.BanditParties: GatherBanditParties(report, origin, radius); break;
                case IntelTopic.Hideouts: GatherHideouts(report, origin, radius); break;
            }

            report.Candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            int playerRelation = notable.GetRelation(Hero.MainHero);
            report.PlayerRelationAtBuild = playerRelation;
            int honor = notable.GetTraitLevel(DefaultTraits.Honor);
            int maxReports = IntelSettings.MaxReports;

            foreach (Sighting s in report.Candidates)
            {
                if (report.Told.Count >= maxReports) break;
                s.Rolled = true;

                float t = Math.Min(1f, s.Distance / radius);
                float know = IntelSettings.KnowChanceNear + (IntelSettings.KnowChanceFar - IntelSettings.KnowChanceNear) * t;
                if (s.IsHideout) know += IntelSettings.HideoutKnowBonus;
                if (s.Kind == SightingKind.Army) know += IntelSettings.ArmyKnowBonus;
                s.KnowChance = Math.Min(1f, know);
                s.Known = IntelSettings.DebugAlwaysKnow || MBRandom.RandomFloat < s.KnowChance;
                if (!s.Known) continue;

                s.LieChance = LieChance(playerRelation, s.TargetRelation, honor);
                s.Lied = MBRandom.RandomFloat < s.LieChance;

                if (s.Lied && MBRandom.RandomFloat < 0.5f)
                {
                    s.Concealed = true; // just leave it out
                    continue;
                }

                if (s.Lied) WriteFalsely(s, origin, village);
                else WriteTruthfully(s, village);
                report.Told.Add(s);
            }

            report.SummaryText = ComposeSummary(report, playerRelation);
            return report;
        }

        /// <summary>Pressed into it: every lie is replaced with the truth, and anything left out is added back.</summary>
        public void Confess()
        {
            foreach (Sighting s in Candidates)
            {
                if (!s.Known || !s.Lied) continue;
                s.WasLie = true;
                s.Lied = false;
                s.Concealed = false;
                WriteTruthfully(s, Village);
            }
            Told.Clear();
            Told.AddRange(Candidates.Where(c => c.Known)); // Candidates are already sorted nearest first
            SummaryText = ComposeSummary(this, PlayerRelationAtBuild);
            Confessed = true;
        }

        private static float LieChance(int playerRelation, int targetRelation, int honor)
        {
            float trust = playerRelation - targetRelation;
            float chance = IntelSettings.BaseLieChance - trust * IntelSettings.LieChancePerPoint;
            if (honor < 0) chance += -honor * IntelSettings.LiePerDishonorLevel;
            else if (honor > 0) chance -= honor * IntelSettings.LiePerDishonorLevel * 0.5f;
            return Math.Max(0f, Math.Min(IntelSettings.MaxLieChance, chance));
        }

        /// <summary>How much a notable likes bandits. Dishonorable notables may be in league with them.</summary>
        internal static int BanditRelation(Hero notable)
        {
            int honor = notable.GetTraitLevel(DefaultTraits.Honor);
            return IntelSettings.BanditBaseRelation - honor * IntelSettings.BanditRelationPerHonor;
        }

        private static int RealmModifier(Settlement village, IFaction partyFaction)
        {
            IFaction home = village.MapFaction;
            if (home == null || partyFaction == null) return 0;
            if (home == partyFaction) return IntelSettings.OwnRealmLoyalty;
            if (FactionManager.IsAtWarAgainstFaction(home, partyFaction)) return -IntelSettings.EnemyRealmHatred;
            return 0;
        }

        private static void GatherEnemyParties(IntelReport report, Vec2 origin, float radius)
        {
            IFaction playerFaction = Hero.MainHero.MapFaction;
            if (playerFaction == null) return;

            foreach (MobileParty p in MobileParty.All)
            {
                if (p == null || !p.IsActive || p.IsMainParty || p.IsDisbanding) continue;
                if (!(p.IsLordParty || p.IsPatrolParty)) continue;
                IFaction faction = p.MapFaction;
                if (faction == null || faction.IsBanditFaction) continue;
                if (!FactionManager.IsAtWarAgainstFaction(faction, playerFaction)) continue;
                if (p.AttachedTo != null) continue;
                if (p.Army != null && p.Army.LeaderParty != p) continue; // the army is reported through its leader

                Vec2 pos = p.GetPosition2D;
                float d = pos.Distance(origin);
                if (d > radius) continue;

                bool isArmy = p.Army != null;
                Hero leader = p.LeaderHero;
                var s = new Sighting
                {
                    Kind = isArmy ? SightingKind.Army : p.IsPatrolParty ? SightingKind.Patrol : SightingKind.LordParty,
                    Label = isArmy ? p.Army.Name : p.Name,
                    FactionName = faction.Name,
                    Position = pos,
                    Distance = d,
                    Men = isArmy ? p.Army.TotalManCount : p.MemberRoster.TotalManCount,
                    TargetRelation = (leader != null ? report.Notable.GetRelation(leader) : 0) + RealmModifier(report.Village, faction)
                };
                GetActivity(p, out s.Activity, out s.ActivityTarget);
                report.Candidates.Add(s);
            }
        }

        private static void GatherBanditParties(IntelReport report, Vec2 origin, float radius)
        {
            int banditRelation = BanditRelation(report.Notable);
            foreach (MobileParty p in MobileParty.AllBanditParties)
            {
                if (p == null || !p.IsActive || p.IsDisbanding) continue;
                if (p.CurrentSettlement != null) continue; // sitting in a hideout: that's the hideout question
                Vec2 pos = p.GetPosition2D;
                float d = pos.Distance(origin);
                if (d > radius) continue;

                var s = new Sighting
                {
                    Kind = SightingKind.BanditParty,
                    Label = p.Name,
                    Position = pos,
                    Distance = d,
                    Men = p.MemberRoster.TotalManCount,
                    TargetRelation = banditRelation
                };
                GetActivity(p, out s.Activity, out s.ActivityTarget);
                report.Candidates.Add(s);
            }
        }

        private static void GatherHideouts(IntelReport report, Vec2 origin, float radius)
        {
            int banditRelation = BanditRelation(report.Notable);
            foreach (Hideout h in Hideout.All)
            {
                if (h == null || !h.IsInfested) continue;
                Settlement hs = h.Settlement;
                if (hs == null) continue;
                Vec2 pos = hs.GetPosition2D;
                float d = pos.Distance(origin);
                if (d > radius) continue;

                int men = 0;
                foreach (MobileParty p in hs.Parties)
                    if (p != null && p.IsBandit) men += p.MemberRoster.TotalManCount;

                report.Candidates.Add(new Sighting
                {
                    Kind = SightingKind.Hideout,
                    Label = hs.Culture?.Name,
                    Hideout = h,
                    Position = pos,
                    Distance = d,
                    Men = men,
                    TargetRelation = banditRelation
                });
            }
        }

        /// <summary>What the party is doing, and the village, castle or town it concerns.</summary>
        private static void GetActivity(MobileParty p, out Activity activity, out Settlement target)
        {
            activity = Activity.None;
            target = null;
            if (p.BesiegedSettlement != null) { activity = Activity.Besieging; target = p.BesiegedSettlement; }
            else if (p.CurrentSettlement != null) { activity = Activity.StayingAt; target = p.CurrentSettlement; }
            else if (p.TargetSettlement != null)
            {
                target = p.TargetSettlement;
                activity = p.DefaultBehavior == AiBehavior.RaidSettlement ? Activity.Raiding
                         : p.DefaultBehavior == AiBehavior.PatrolAroundPoint ? Activity.Patrolling
                         : Activity.HeadingFor;
            }
            // Only name proper places (never a hideout or other odd settlement).
            if (target != null && !IsLandmark(target)) { activity = Activity.None; target = null; }
        }

        private static bool IsLandmark(Settlement st) => st != null && (st.IsVillage || st.IsTown || st.IsCastle);

        // ------------------------------------------------------------------ wording
        // Every player-facing sentence is a TextObject with a {=vi_...} id, so translators can
        // override it from ModuleData/Languages without touching the code.

        private static int Fuzz(int men) => (int)Math.Round(men * MBRandom.RandomFloatRanged(0.9f, 1.1f));

        private static void WriteTruthfully(Sighting s, Settlement village)
        {
            Write(s, village, Fuzz(s.Men), NearestLandmark(s.Position), s.Activity, s.ActivityTarget);
        }

        /// <summary>The wrong head count, the wrong place and the wrong destination.</summary>
        private static void WriteFalsely(Sighting s, Vec2 origin, Settlement village)
        {
            Settlement realNear = NearestLandmark(s.Position);
            Settlement fakeNear = RandomLandmarkAround(origin, village, realNear, s.ActivityTarget) ?? realNear;

            Activity activity = Activity.None;
            Settlement fakeTarget = null;
            if (!s.IsHideout && (s.ActivityTarget != null || MBRandom.RandomFloat < 0.5f))
            {
                fakeTarget = RandomLandmarkAround(origin, village, realNear, s.ActivityTarget, fakeNear);
                if (fakeTarget != null) activity = Activity.HeadingFor;
            }

            int men = (int)Math.Round(s.Men * (MBRandom.RandomFloat < 0.5f
                ? MBRandom.RandomFloatRanged(0.35f, 0.7f)    // "nothing to worry about"
                : MBRandom.RandomFloatRanged(1.5f, 2.5f)));  // "stay well away"
            Write(s, village, Math.Max(1, men), fakeNear, activity, fakeTarget);
        }

        private static void Write(Sighting s, Settlement village, int men, Settlement near, Activity activity, Settlement target)
        {
            TextObject size = SizeText(men);
            TextObject label = s.Label ?? new TextObject("{=vi_unknown_bandits}bandits");

            // Short forms for the summary and the follow-up question.
            TextObject summary, reference;
            switch (s.Kind)
            {
                case SightingKind.Army:
                    summary = new TextObject("{=vi_kind_army}an army of {SIZE}");
                    reference = new TextObject("{=vi_ref_army}the army of {SIZE}");
                    break;
                case SightingKind.Patrol:
                    summary = new TextObject("{=vi_kind_patrol}a patrol of {SIZE}");
                    reference = new TextObject("{=vi_ref_patrol}the patrol of {SIZE}");
                    break;
                case SightingKind.LordParty:
                    summary = new TextObject("{=vi_kind_lord}a lord's war party of {SIZE}");
                    reference = new TextObject("{=vi_ref_lord}the war party of {SIZE}");
                    break;
                case SightingKind.BanditParty:
                    summary = new TextObject("{=vi_kind_bandits}a band of {BANDITS}, {SIZE}");
                    reference = new TextObject("{=vi_ref_bandits}the {BANDITS}, {SIZE}");
                    break;
                default:
                    summary = men > 0
                        ? new TextObject("{=vi_kind_hideout}a hideout of {BANDITS} with {SIZE}")
                        : new TextObject("{=vi_kind_hideout_empty}a hideout of {BANDITS}");
                    reference = new TextObject("{=vi_ref_hideout}the {BANDITS} hideout");
                    break;
            }
            summary.SetTextVariable("SIZE", size).SetTextVariable("BANDITS", label);
            reference.SetTextVariable("SIZE", size).SetTextVariable("BANDITS", label);
            s.SummaryText = summary.ToString();
            s.RefText = reference.ToString();

            // The full answer, given only when the player asks about this one.
            TextObject where = WhereText(near, village);
            TextObject act = ActivityText(activity, target);
            TextObject detail;
            if (s.IsHideout)
            {
                detail = new TextObject("{=vi_detail_hideout}It's out in the wilds {WHERE}.");
            }
            else if (s.IsBanditParty)
            {
                detail = act != null
                    ? new TextObject("{=vi_detail_bandits_going}They were prowling {WHERE}, {ACTIVITY}.")
                    : new TextObject("{=vi_detail_bandits_seen}They were prowling {WHERE}.");
            }
            else
            {
                bool actIsLocation = act != null && (activity == Activity.StayingAt || activity == Activity.Besieging);
                detail = actIsLocation ? new TextObject("{=vi_detail_party_at}That's {PARTY} of {FACTION}. They're {ACTIVITY}.")
                       : act != null ? new TextObject("{=vi_detail_party_going}That's {PARTY} of {FACTION}. They were seen {WHERE}, {ACTIVITY}.")
                       : new TextObject("{=vi_detail_party_seen}That's {PARTY} of {FACTION}. They were seen {WHERE}.");
                detail.SetTextVariable("PARTY", label);
                detail.SetTextVariable("FACTION", s.FactionName ?? new TextObject("{=vi_unknown_realm}an enemy realm"));
            }
            detail.SetTextVariable("WHERE", where);
            if (act != null) detail.SetTextVariable("ACTIVITY", act);
            s.DetailText = detail.ToString();
        }

        internal static TextObject WhereText(Settlement near, Settlement village)
        {
            if (near == null) return new TextObject("{=vi_where_unknown}not far from here");
            if (near == village) return new TextObject("{=vi_where_here}close to this village");
            return new TextObject("{=vi_where_near}near {SETTLEMENT}").SetTextVariable("SETTLEMENT", near.Name);
        }

        internal static TextObject ActivityText(Activity activity, Settlement target)
        {
            if (target == null) return null;
            TextObject t;
            switch (activity)
            {
                case Activity.Raiding: t = new TextObject("{=vi_act_raid}on their way to raid {SETTLEMENT}"); break;
                case Activity.Patrolling: t = new TextObject("{=vi_act_patrol}patrolling around {SETTLEMENT}"); break;
                case Activity.Besieging: t = new TextObject("{=vi_act_besiege}besieging {SETTLEMENT}"); break;
                case Activity.StayingAt: t = new TextObject("{=vi_act_staying}staying at {SETTLEMENT}"); break;
                case Activity.HeadingFor: t = new TextObject("{=vi_act_heading}heading for {SETTLEMENT}"); break;
                default: return null;
            }
            return t.SetTextVariable("SETTLEMENT", target.Name);
        }

        private static TextObject SizeText(int men)
        {
            if (men <= 6) return new TextObject("{=vi_size_handful}a handful of men");
            int rounded = men <= 100 ? (int)(Math.Round(men / 5.0) * 5) : (int)(Math.Round(men / 10.0) * 10);
            return new TextObject("{=vi_size_about}about {COUNT} men").SetTextVariable("COUNT", rounded);
        }

        /// <summary>The nearest village, castle or town to a spot on the map (may be the asking village itself).</summary>
        internal static Settlement NearestLandmark(Vec2 pos)
        {
            Settlement best = null;
            float bestD = float.MaxValue;
            foreach (Settlement st in Settlement.All)
            {
                if (!IsLandmark(st)) continue;
                float d = st.GetPosition2D.Distance(pos);
                if (d < bestD) { bestD = d; best = st; }
            }
            return best;
        }

        /// <summary>A random village, castle or town in the area that isn't one of <paramref name="exclude"/>. Used for lies.</summary>
        private static Settlement RandomLandmarkAround(Vec2 origin, Settlement village, params Settlement[] exclude)
        {
            float radius = IntelSettings.SearchRadius * 1.25f;
            var options = new List<Settlement>();
            foreach (Settlement st in Settlement.All)
            {
                if (!IsLandmark(st) || st == village || Array.IndexOf(exclude, st) >= 0) continue;
                if (st.GetPosition2D.Distance(origin) <= radius) options.Add(st);
            }
            return options.Count == 0 ? null : options[MBRandom.RandomInt(options.Count)];
        }

        /// <summary>"I've seen an army of about 300 men, a patrol of about 30 men, and a lord's war party of about 60 men."</summary>
        private static string ComposeSummary(IntelReport r, int playerRelation)
        {
            if (r.Told.Count == 0)
            {
                switch (r.Topic)
                {
                    case IntelTopic.EnemyParties: return new TextObject("{=vi_none_enemy}I haven't seen any war bands around here lately. It's been quiet, thank the heavens.").ToString();
                    case IntelTopic.BanditParties: return new TextObject("{=vi_none_bandit}No bandits that I know of. Not around here, anyway.").ToString();
                    default: return new TextObject("{=vi_none_hideout}If those scum have a camp near here, I don't know where it is.").ToString();
                }
            }

            string list;
            if (r.Told.Count == 1)
                list = r.Told[0].SummaryText;
            else
            {
                string head = string.Join(new TextObject("{=vi_list_separator}, ").ToString(), r.Told.Take(r.Told.Count - 1).Select(s => s.SummaryText));
                list = new TextObject("{=vi_list_last}{ITEMS}, and {LAST}")
                    .SetTextVariable("ITEMS", head)
                    .SetTextVariable("LAST", r.Told[r.Told.Count - 1].SummaryText).ToString();
            }

            TextObject summary;
            switch (r.Topic)
            {
                case IntelTopic.EnemyParties: summary = new TextObject("{=vi_summary_enemy}I've seen {LIST}."); break;
                case IntelTopic.BanditParties: summary = new TextObject("{=vi_summary_bandit}There's been {LIST} prowling about."); break;
                default: summary = new TextObject("{=vi_summary_hideout}I know of {LIST}."); break;
            }
            summary.SetTextVariable("LIST", list);

            if (playerRelation >= 10)
                summary = new TextObject("{=vi_summary_friend}Keep this between us, friend. {SUMMARY} Ask if you want to know more about any of them.").SetTextVariable("SUMMARY", summary);
            else
                summary = new TextObject("{=vi_summary_plain}Keep this between us. {SUMMARY} Ask if you want to know more.").SetTextVariable("SUMMARY", summary);
            return summary.ToString();
        }

        // ------------------------------------------------------------------ debug

        /// <summary>Debug mode: show in the chat what was true, what was a lie and what was left out.</summary>
        public void PrintDebug(bool repeat)
        {
            if (!IntelSettings.DebugMode) return;
            DebugChat.Say($"{Notable.Name} ({Topic}{(repeat ? ", repeating earlier answer" : "")}): relation with you {Notable.GetRelation(Hero.MainHero)}, honor {Notable.GetTraitLevel(DefaultTraits.Honor)}, {Candidates.Count} in range.", Colors.Magenta);

            int shown = 0;
            foreach (Sighting s in Candidates)
            {
                if (!s.Rolled) continue;
                shown++;
                string label = s.Label?.ToString() ?? "bandits";
                if (s.IsHideout) label = "Hideout of " + label;
                string truth = $"{label} ({s.Kind}): really {s.Men} men {WhereText(NearestLandmark(s.Position), Village)}" +
                               (s.ActivityTarget != null ? ", " + ActivityText(s.Activity, s.ActivityTarget) : "");

                if (!s.Known)
                    DebugChat.Say($"DIDN'T KNOW ({s.KnowChance:0%} chance to know). {truth}.", Colors.Gray);
                else if (s.WasLie)
                    DebugChat.Say($"LIED, then CONFESSED after being pressed. {truth}.", Colors.Yellow);
                else if (s.Concealed)
                    DebugChat.Say($"LIED, left it out ({s.LieChance:0%} lie chance, relation with them {s.TargetRelation}). {truth}.", Colors.Red);
                else if (s.Lied)
                    DebugChat.Say($"LIED ({s.LieChance:0%} lie chance, relation with them {s.TargetRelation}). {truth}. They'll say: \"{s.SummaryText}\" / \"{s.DetailText}\"", Colors.Red);
                else
                    DebugChat.Say($"TRUTH ({s.LieChance:0%} lie chance, relation with them {s.TargetRelation}). {truth}.", Colors.Green);
            }
            if (shown == 0) DebugChat.Say("Nothing in range.", Colors.Gray);
        }
    }

    internal static class DebugChat
    {
        public static void Say(string text, Color color)
        {
            InformationManager.DisplayMessage(new InformationMessage("[Village Intel debug] " + text, color));
        }
    }
}
