using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace VillageIntel
{
    /// <summary>
    /// Adds "What news is there of trouble around here?" to conversations with village notables.
    /// <list type="bullet">
    /// <item>Three questions (enemy war parties, bandit gangs, bandit hideouts), each paid for separately
    /// with gold or goods, or talked round with the vanilla persuasion system.</item>
    /// <item>The notable answers with a short list (kind of party and head count); the player can then ask
    /// about each one for where it was seen and where it was going.</item>
    /// <item>The notable may not know, or may lie, depending on how they feel about the player compared with
    /// the party in question. A player whose Charm, Scouting or Roguery beats theirs sees through the lie and
    /// can press them for the truth.</item>
    /// </list>
    /// Nothing is saved: the "asked recently" memory resets when a save is loaded.
    /// </summary>
    public sealed class NotableIntelBehavior : CampaignBehaviorBase
    {
        private const int MaxDetailSlots = 8; // matches the "Most sightings per answer" maximum

        private sealed class Pending
        {
            public IntelReport Report;
            public bool Free;
            public bool Repeat;          // same question within the cooldown, already answered
            public bool FreeByFriendship;
            public bool Persuaded;
            public int Price;
            public bool AcceptGold;
            public bool AcceptGoods;
            public EquipmentElement Goods;
            public int GoodsAmount;
            public TextObject GoodsName;
        }

        private readonly Dictionary<Hero, Dictionary<IntelTopic, IntelReport>> _history = new Dictionary<Hero, Dictionary<IntelTopic, IntelReport>>();
        private Pending _pending;
        private IntelReport _current;    // the answer being discussed, for the follow-up questions
        private int _detailIndex = -1;

        private bool _lastDetailWasLie;  // the follow-up answer just given was a lie the player can see through
        private bool _pressSucceeded;

        private PersuasionTask _task;
        private PersuasionDifficulty _difficulty;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Intentionally empty: keep the mod out of save files.
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _history.Clear();
            _pending = null;
            _current = null;
            AddDialogs(starter);
        }

        // ------------------------------------------------------------------ dialogue tree
        // Every line has a {=vi_...} id so it can be translated from ModuleData/Languages.

        private void AddDialogs(CampaignGameStarter s)
        {
            // Entry point in the notable's main conversation menu.
            s.AddPlayerLine("vi_ask", "hero_main_options", "vi_hub",
                "{=vi_ask}What news is there of trouble around here?", CanAsk, null, 100);

            // Notable refuses outright (bad relation).
            s.AddDialogLine("vi_hub_refuse", "vi_hub", "vi_refused", "{=!}{VI_REFUSE}", RefuseCondition, null, 110);
            s.AddPlayerLine("vi_refused_ok", "vi_refused", "lord_pretalk", "{=vi_refused_ok}Suit yourself.", null, null, 100);

            // Notable is willing to listen. Three separate questions, each paid for separately.
            s.AddDialogLine("vi_hub", "vi_hub", "vi_topics", "{=!}{VI_HUB}", HubCondition, null, 100);
            s.AddPlayerLine("vi_topic_enemy", "vi_topics", "vi_quote",
                "{=vi_topic_enemy}Have any enemy war parties been seen near here?", null, () => Prepare(IntelTopic.EnemyParties), 100);
            s.AddPlayerLine("vi_topic_bandit", "vi_topics", "vi_quote",
                "{=vi_topic_bandit}Have there been bandits on the roads?", null, () => Prepare(IntelTopic.BanditParties), 100);
            s.AddPlayerLine("vi_topic_hideout", "vi_topics", "vi_quote",
                "{=vi_topic_hideout}Do you know where the bandits make camp?", null, () => Prepare(IntelTopic.Hideouts), 100);
            s.AddPlayerLine("vi_topic_cancel", "vi_topics", "lord_pretalk", "{=vi_topic_cancel}Never mind.", null, null, 100);

            // Free answer (friend, honest "nothing", or repeating an earlier answer).
            s.AddDialogLine("vi_quote_free", "vi_quote", "vi_details", "{=!}{VI_ANSWER}", FreeAnswerCondition, DeliverSummary, 110);

            // Notable hints that money would help, without naming a sum.
            s.AddDialogLine("vi_quote_price", "vi_quote", "vi_pay", "{=!}{VI_QUOTE}", PriceCondition, null, 100);
            s.AddPlayerLine("vi_pay_gold", "vi_pay", "vi_paid", "{=vi_pay_gold}Here, take {VI_GOLD}{GOLD_ICON}.",
                PayGoldCondition, PayGold, 100, CanPayGold);
            s.AddPlayerLine("vi_pay_goods", "vi_pay", "vi_paid", "{=vi_pay_goods}Take {VI_GOODS} from my wagons.",
                PayGoodsCondition, PayGoods, 100);
            s.AddPlayerLine("vi_persuade", "vi_pay", "vi_persuade_hub", "{=vi_persuade}Surely we can come to an understanding without coin.",
                PersuadeCondition, StartPersuasion, 100);
            s.AddPlayerLine("vi_pay_decline", "vi_pay", "lord_pretalk", "{=vi_pay_decline}I'll find out some other way.",
                null, () => _pending = null, 100);

            AddPersuasionDialogs(s);

            // Paid (or persuaded) answer.
            s.AddDialogLine("vi_answer", "vi_paid", "vi_details", "{=!}{VI_ANSWER}", PaidAnswerCondition, DeliverSummary, 100);

            // Follow-up questions: one per sighting in the summary.
            for (int i = 0; i < MaxDetailSlots; i++)
            {
                int slot = i;
                s.AddPlayerLine("vi_detail_" + slot, "vi_details", "vi_detail_answer", "{=!}{VI_DETAIL_Q_" + slot + "}",
                    () => DetailQuestionCondition(slot), () => { _detailIndex = slot; _lastDetailWasLie = false; }, 100);
            }
            s.AddPlayerLine("vi_press", "vi_details", "vi_press_result", "{=!}{VI_PRESS_LINE}", PressCondition, Press, 95);
            s.AddDialogLine("vi_press_ok", "vi_press_result", "vi_details", "{=!}{VI_PRESS_OK}", PressSucceededCondition, null, 110);
            s.AddDialogLine("vi_press_fail", "vi_press_result", "vi_details", "{=!}{VI_PRESS_FAIL}", PressFailedCondition, null, 100);
            s.AddPlayerLine("vi_details_done", "vi_details", "lord_pretalk", "{=!}{VI_DONE}", DoneCondition, () => _current = null, 90);
            s.AddDialogLine("vi_detail_answer", "vi_detail_answer", "vi_details", "{=!}{VI_DETAIL}", DetailAnswerCondition, DetailAnswerConsequence, 100);
        }

        // ------------------------------------------------------------------ conditions

        private static Settlement NotableSettlement(Hero h) => h?.CurrentSettlement ?? Settlement.CurrentSettlement;

        private bool CanAsk()
        {
            try
            {
                if (!IntelSettings.Enabled) return false;
                Hero h = Hero.OneToOneConversationHero;
                if (h == null || h == Hero.MainHero || !h.IsNotable) return false;
                Settlement st = NotableSettlement(h);
                if (st == null) return false;
                return st.IsVillage || (IntelSettings.AllowTownNotables && st.IsTown);
            }
            catch (Exception e) { Error(e); return false; }
        }

        private bool RefuseCondition()
        {
            Hero h = Hero.OneToOneConversationHero;
            if (h == null || h.GetRelation(Hero.MainHero) > IntelSettings.RefuseRelation) return false;
            MBTextManager.SetTextVariable("VI_REFUSE", MBRandom.RandomFloat < 0.5f
                ? new TextObject("{=vi_refuse_1}Trouble? The only trouble I see is standing in front of me. I've nothing to say to you.")
                : new TextObject("{=vi_refuse_2}Ask someone else. I don't help the likes of you."));
            return true;
        }

        private bool HubCondition()
        {
            Hero h = Hero.OneToOneConversationHero;
            if (h == null) return false;
            int rel = h.GetRelation(Hero.MainHero);
            TextObject line = rel >= IntelSettings.FreeRelation ? new TextObject("{=vi_hub_friend}For you? Ask away. What do you want to know?")
                            : rel >= 0 ? new TextObject("{=vi_hub_neutral}Depends what kind of trouble you mean.")
                            : new TextObject("{=vi_hub_cold}Hmph. What is it you want to know?");
            MBTextManager.SetTextVariable("VI_HUB", line);
            return true;
        }

        // ------------------------------------------------------------------ preparing an answer

        private void Prepare(IntelTopic topic)
        {
            _pending = null;
            _current = null;
            _lastDetailWasLie = false;
            try
            {
                Hero notable = Hero.OneToOneConversationHero;
                Settlement village = NotableSettlement(notable);
                if (notable == null || village == null) return;

                if (!_history.TryGetValue(notable, out var byTopic))
                    _history[notable] = byTopic = new Dictionary<IntelTopic, IntelReport>();

                // Asked recently: reuse the same answer so re-asking can't reroll it.
                if (!IntelSettings.DebugNoCooldown
                    && byTopic.TryGetValue(topic, out IntelReport old)
                    && old.Village == village
                    && old.CreatedAt.ElapsedHoursUntilNow < IntelSettings.CooldownHours)
                {
                    if (old.Delivered)
                    {
                        _pending = new Pending { Report = old, Free = true, Repeat = true };
                        return;
                    }
                    _pending = new Pending { Report = old };
                }
                else
                {
                    IntelReport report = IntelReport.Build(notable, village, topic);
                    byTopic[topic] = report;
                    _pending = new Pending { Report = report };
                }

                SetUpPrice(_pending, notable);
            }
            catch (Exception e) { Error(e); _pending = null; }
        }

        private static int BasePrice(IntelTopic topic)
        {
            switch (topic)
            {
                case IntelTopic.EnemyParties: return IntelSettings.EnemyBasePrice;
                case IntelTopic.BanditParties: return IntelSettings.BanditBasePrice;
                default: return IntelSettings.HideoutBasePrice;
            }
        }

        private static void SetUpPrice(Pending p, Hero notable)
        {
            int rel = notable.GetRelation(Hero.MainHero);
            if (rel >= IntelSettings.FreeRelation)
            {
                p.Free = true;
                p.FreeByFriendship = true;
                return;
            }
            // An honest notable who knows nothing won't take your money for it. A crooked one will.
            if (p.Report.KnowsNothingHonestly && notable.GetTraitLevel(DefaultTraits.Honor) >= 0)
            {
                p.Free = true;
                return;
            }

            int basePrice = BasePrice(p.Report.Topic);
            if (basePrice <= 0)
            {
                p.Free = true;
                return;
            }

            float price = basePrice;
            if (rel < 0) price *= 1f + (-rel) * IntelSettings.PricePerBadRelation;
            else price *= Math.Max(0.2f, 1f - rel * IntelSettings.PricePerBadRelation * 0.5f);

            int generosity = notable.GetTraitLevel(DefaultTraits.Generosity);
            price *= Math.Max(0.4f, 1f - 0.15f * generosity);

            float charm = Hero.MainHero.GetSkillValue(DefaultSkills.Charm);
            price *= 1f - Math.Min(1f, charm / 300f) * IntelSettings.MaxCharmDiscount;

            p.Price = Math.Max(5, (int)(Math.Round(price / 5f) * 5));
            p.AcceptGold = true;
            if (MBRandom.RandomFloat < IntelSettings.GoodsChance && PickGoods(p))
            {
                p.AcceptGoods = true;
                p.AcceptGold = MBRandom.RandomFloat >= IntelSettings.GoodsOnlyChance;
            }
        }

        /// <summary>Pick food or trade goods from the player's inventory worth about the asking price.</summary>
        private static bool PickGoods(Pending p)
        {
            ItemRoster roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null) return false;

            var options = new List<(EquipmentElement element, int amount, float weight)>();
            float totalWeight = 0f;
            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement el = roster.GetElementCopyAtIndex(i);
                ItemObject item = el.EquipmentElement.Item;
                if (item == null || el.Amount <= 0 || el.EquipmentElement.IsQuestItem) continue;
                if (!(item.IsFood || item.IsTradeGood) || item.Value <= 0) continue;

                int need = Math.Max(1, (int)Math.Ceiling(p.Price * IntelSettings.GoodsMarkup / item.Value));
                if (need > el.Amount) continue;

                float w = item.IsFood ? 2f : 1f; // villagers like food best
                options.Add((el.EquipmentElement, need, w));
                totalWeight += w;
            }
            if (options.Count == 0) return false;

            float roll = MBRandom.RandomFloat * totalWeight;
            int pick = options.Count - 1;
            for (int i = 0; i < options.Count; i++)
            {
                roll -= options[i].weight;
                if (roll <= 0f) { pick = i; break; }
            }
            p.Goods = options[pick].element;
            p.GoodsAmount = options[pick].amount;
            p.GoodsName = new TextObject("{=vi_goods}{AMOUNT} {ITEM}")
                .SetTextVariable("AMOUNT", p.GoodsAmount)
                .SetTextVariable("ITEM", p.Goods.Item.Name);
            return true;
        }

        // ------------------------------------------------------------------ quoting and paying

        private bool FreeAnswerCondition()
        {
            Pending p = _pending;
            if (p == null || !p.Free) return false;

            TextObject text;
            if (p.Repeat) text = new TextObject("{=vi_repeat}As I told you before. {ANSWER}");
            else if (p.FreeByFriendship) text = new TextObject("{=vi_free_friend}For a friend, no charge. {ANSWER}");
            else if (p.Report.Told.Count == 0) text = new TextObject("{=vi_keep_coin}{ANSWER} Keep your coin.");
            else text = new TextObject("{=!}{ANSWER}");
            text.SetTextVariable("ANSWER", p.Report.SummaryText);
            MBTextManager.SetTextVariable("VI_ANSWER", text);
            return true;
        }

        /// <summary>The notable explains the risk and hints that payment would help. No sum is named.</summary>
        private bool PriceCondition()
        {
            Pending p = _pending;
            if (p == null || p.Free) return false;

            TextObject risk;
            switch (p.Report.Topic)
            {
                case IntelTopic.EnemyParties:
                    risk = new TextObject("{=vi_risk_enemy}If the lords found out I'd been telling strangers about their movements, it'd be my neck.");
                    break;
                case IntelTopic.BanditParties:
                    risk = new TextObject("{=vi_risk_bandits}Those cutthroats have friends in every village. If word got out that I talked, I'd end up in a ditch.");
                    break;
                default:
                    risk = new TextObject("{=vi_risk_hideout}Tell you where their camp is? If they ever learned it was me, they'd burn my house down with my family in it.");
                    break;
            }

            TextObject offer;
            if (p.AcceptGold && p.AcceptGoods)
                offer = new TextObject("{=vi_offer_either}Still... some coin, or some supplies from your wagons, could change my mind.");
            else if (p.AcceptGoods)
                offer = new TextObject("{=vi_offer_goods}Coin won't fill our granary, but some supplies from your wagons might change my mind.");
            else
                offer = new TextObject("{=vi_offer_gold}Still... some coin could change my mind.");

            MBTextManager.SetTextVariable("VI_QUOTE", new TextObject("{=vi_quote}{RISK} {OFFER}")
                .SetTextVariable("RISK", risk)
                .SetTextVariable("OFFER", offer));
            return true;
        }

        private bool PayGoldCondition()
        {
            Pending p = _pending;
            if (p == null || p.Free || p.Persuaded || !p.AcceptGold) return false;
            MBTextManager.SetTextVariable("VI_GOLD", p.Price);
            return true;
        }

        private bool CanPayGold(out TextObject explanation)
        {
            explanation = null;
            Pending p = _pending;
            if (p != null && !IntelSettings.DebugMode && Hero.MainHero.Gold < p.Price)
            {
                explanation = new TextObject("{=vi_not_enough_gold}You don't have enough denars.");
                return false;
            }
            return true;
        }

        private bool PayGoodsCondition()
        {
            Pending p = _pending;
            if (p == null || p.Free || p.Persuaded || !p.AcceptGoods) return false;
            MBTextManager.SetTextVariable("VI_GOODS", p.GoodsName);
            return true;
        }

        private void PayGold()
        {
            Pending p = _pending;
            if (p == null) return;
            try
            {
                if (IntelSettings.DebugMode)
                {
                    DebugChat.Say($"Not charged. You would have paid {p.Price} denars to {p.Report.Notable.Name}" +
                                  (IntelSettings.RelationOnPay > 0 ? $" and gained {IntelSettings.RelationOnPay} relation." : "."), Colors.Yellow);
                    return;
                }
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, p.Report.Notable, p.Price, false);
                AfterPayment(p);
            }
            catch (Exception e) { Error(e); }
        }

        private void PayGoods()
        {
            Pending p = _pending;
            if (p == null) return;
            try
            {
                if (IntelSettings.DebugMode)
                {
                    DebugChat.Say($"Not charged. You would have given {p.GoodsName} (about {p.GoodsAmount * p.Goods.Item.Value} denars of base value) to {p.Report.Notable.Name}" +
                                  (IntelSettings.RelationOnPay > 0 ? $" and gained {IntelSettings.RelationOnPay} relation." : "."), Colors.Yellow);
                    return;
                }
                MobileParty.MainParty.ItemRoster.AddToCounts(p.Goods, -p.GoodsAmount);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=vi_gave_goods}You gave {GOODS} to {NOTABLE}.")
                        .SetTextVariable("GOODS", p.GoodsName)
                        .SetTextVariable("NOTABLE", p.Report.Notable.Name).ToString(),
                    Colors.Yellow));
                AfterPayment(p);
            }
            catch (Exception e) { Error(e); }
        }

        private static void AfterPayment(Pending p)
        {
            if (IntelSettings.RelationOnPay > 0)
                ChangeRelationAction.ApplyPlayerRelation(p.Report.Notable, IntelSettings.RelationOnPay, false, true);
        }

        private bool PaidAnswerCondition()
        {
            Pending p = _pending;
            if (p == null) return false;
            MBTextManager.SetTextVariable("VI_ANSWER", p.Report.SummaryText);
            return true;
        }

        // ------------------------------------------------------------------ persuasion (vanilla system)
        // Same wiring as the game's own issue persuasions: ConversationManager runs the rolls, perks,
        // progress bar and option blocking; we supply the task and the reaction lines, and award skill
        // experience on success the way vanilla's defection persuasion does.

        private void AddPersuasionDialogs(CampaignGameStarter s)
        {
            s.AddDialogLine("vi_persuade_success", "vi_persuade_hub", "vi_persuaded",
                "{=vi_persuade_success}...Fine. You make a fair point. But you never heard any of this from me.",
                () => _task != null && ConversationManager.GetPersuasionProgressSatisfied(), PersuasionSucceeded, 120);
            s.AddDialogLine("vi_persuade_failed", "vi_persuade_hub", "vi_pay", "{=!}{VI_PERSUADE_FAIL}",
                PersuasionFailedCondition, PersuasionFailed, 110);
            s.AddDialogLine("vi_persuade_attempt", "vi_persuade_hub", "vi_persuade_options", "{=!}{VI_PERSUADE_PROMPT}",
                () =>
                {
                    if (_task == null) return false;
                    MBTextManager.SetTextVariable("VI_PERSUADE_PROMPT", _task.SpokenLine);
                    return true;
                }, null, 100);

            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                s.AddPlayerLine("vi_persuade_option_" + slot, "vi_persuade_options", "vi_persuade_reaction", "{=!}{VI_PERSUADE_OPTION_" + slot + "}",
                    () => PersuasionOptionCondition(slot),
                    () => { if (_task != null && _task.Options.Count > slot) _task.Options[slot].BlockTheOption(true); },
                    100,
                    (out TextObject hint) => PersuasionOptionClickable(slot, out hint),
                    () => _task != null && _task.Options.Count > slot ? _task.Options[slot] : null);
            }
            s.AddPlayerLine("vi_persuade_cancel", "vi_persuade_options", "vi_pay", "{=vi_persuade_cancel}Forget it. Let's talk about payment.",
                null, () => { _task = null; ConversationManager.EndPersuasion(); }, 90);

            s.AddDialogLine("vi_persuade_reaction", "vi_persuade_reaction", "vi_persuade_hub", "{=!}{VI_PERSUADE_REACTION}",
                PersuasionReactionCondition, PersuasionReactionConsequence, 100);

            s.AddPlayerLine("vi_persuaded_go_on", "vi_persuaded", "vi_paid", "{=vi_persuaded_go_on}You have my word. Go on.", null, null, 100);
        }

        private bool PersuadeCondition()
        {
            Pending p = _pending;
            return IntelSettings.AllowPersuasion && p != null && !p.Free && !p.Persuaded && !p.Report.PersuasionTried;
        }

        private void StartPersuasion()
        {
            Pending p = _pending;
            if (p == null) return;
            try
            {
                p.Report.PersuasionTried = true;
                int rel = p.Report.Notable.GetRelation(Hero.MainHero);
                _difficulty = rel >= 15 ? PersuasionDifficulty.EasyMedium
                            : rel >= 0 ? PersuasionDifficulty.Medium
                            : rel >= -15 ? PersuasionDifficulty.MediumHard
                            : PersuasionDifficulty.Hard;
                _task = CreateTask();
                ConversationManager.StartPersuasion(2f, 1f, 0f, 2f, 2f, 0f, _difficulty);
            }
            catch (Exception e) { Error(e); _task = null; }
        }

        private static PersuasionTask CreateTask()
        {
            var task = new PersuasionTask(0)
            {
                SpokenLine = new TextObject("{=vi_pers_prompt}Talk, then. Why should I stick my neck out for you?"),
                FinalFailLine = new TextObject("{=vi_pers_fail}No. Fine words won't keep me safe. Pay, or find someone else to ask."),
                TryLaterLine = new TextObject("{=vi_pers_later}Not today.")
            };
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive,
                PersuasionArgumentStrength.Normal, false,
                new TextObject("{=vi_pers_charm}Help me now and I'll remember it. A friend like me is worth more than a few coins.")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Valor, TraitEffect.Positive,
                PersuasionArgumentStrength.Hard, true,
                new TextObject("{=vi_pers_valor}Tell me where they are and I'll deal with them myself. That keeps your people safer than any purse could.")));
            task.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Calculating, TraitEffect.Positive,
                PersuasionArgumentStrength.Normal, false,
                new TextObject("{=vi_pers_roguery}Nobody will ever know it came from you. I never name my sources.")));
            return task;
        }

        private bool PersuasionOptionCondition(int slot)
        {
            if (_task == null || _task.Options.Count <= slot) return false;
            PersuasionOptionArgs option = _task.Options[slot];
            // Vanilla's "{line} {success chance}" text.
            TextObject line = new TextObject("{=bSo9hKwr}{PERSUASION_OPTION_LINE} {SUCCESS_CHANCE}");
            line.SetTextVariable("SUCCESS_CHANCE", Helpers.PersuasionHelper.ShowSuccess(option, true));
            line.SetTextVariable("PERSUASION_OPTION_LINE", option.Line);
            MBTextManager.SetTextVariable("VI_PERSUADE_OPTION_" + slot, line);
            return true;
        }

        private bool PersuasionOptionClickable(int slot, out TextObject hint)
        {
            hint = new TextObject("{=9ACJsI6S}Blocked");
            if (_task == null || _task.Options.Count <= slot) return false;
            if (_task.Options[slot].IsBlocked) return false;
            hint = null;
            return true;
        }

        private bool PersuasionReactionCondition()
        {
            var last = ConversationManager.GetPersuasionChosenOptions().LastOrDefault();
            if (last == null) return false;
            MBTextManager.SetTextVariable("VI_PERSUADE_REACTION", Helpers.PersuasionHelper.GetDefaultPersuasionOptionReaction(last.Item2));
            if (last.Item2 == PersuasionOptionResult.CriticalFailure) _task?.BlockAllOptions();
            return true;
        }

        private void PersuasionReactionConsequence()
        {
            try
            {
                var last = ConversationManager.GetPersuasionChosenOptions().LastOrDefault();
                if (last == null || _task == null) return;
                var model = Campaign.Current.Models.PersuasionModel;
                float difficulty = model.GetDifficulty(_difficulty);
                model.GetEffectChances(last.Item1, out float moveToNextStage, out float blockRandom, difficulty);
                _task.ApplyEffects(moveToNextStage, blockRandom);
            }
            catch (Exception e) { Error(e); }
        }

        private bool PersuasionFailedCondition()
        {
            if (_task == null) return false;
            if (!_task.Options.All(o => o.IsBlocked) || ConversationManager.GetPersuasionProgressSatisfied()) return false;
            MBTextManager.SetTextVariable("VI_PERSUADE_FAIL", _task.FinalFailLine);
            return true;
        }

        private void PersuasionFailed()
        {
            _task = null;
            ConversationManager.EndPersuasion();
        }

        private void PersuasionSucceeded()
        {
            try
            {
                // Skill experience for each argument that landed, as vanilla does after a successful persuasion.
                foreach (var chosen in ConversationManager.GetPersuasionChosenOptions().ToList())
                {
                    if (chosen.Item2 == PersuasionOptionResult.Success || chosen.Item2 == PersuasionOptionResult.CriticalSuccess)
                        SkillLevelingManager.OnPersuasionSucceeded(Hero.MainHero, chosen.Item1.SkillUsed, _difficulty, Math.Abs((int)chosen.Item1.ArgumentStrength));
                }
                if (_pending != null) _pending.Persuaded = true;
                if (IntelSettings.DebugMode) DebugChat.Say("Persuasion succeeded: the answer is free.", Colors.Yellow);
            }
            catch (Exception e) { Error(e); }
            finally
            {
                _task = null;
                ConversationManager.EndPersuasion();
            }
        }

        // ------------------------------------------------------------------ after the summary

        private void DeliverSummary()
        {
            Pending p = _pending;
            _pending = null;
            if (p == null) return;
            try
            {
                IntelReport r = p.Report;
                _current = r;
                r.PrintDebug(p.Repeat);
                if (p.Repeat || r.Delivered) return;
                r.Delivered = true;

                // A sharper mind than the notable's can tell when they're being played.
                if (r.LieCount > 0) TryNoticeLie(r);
            }
            catch (Exception e) { Error(e); }
        }

        private bool DetailQuestionCondition(int slot)
        {
            IntelReport r = _current;
            if (r == null || slot >= r.Told.Count) return false;
            MBTextManager.SetTextVariable("VI_DETAIL_Q_" + slot,
                new TextObject("{=vi_ask_detail}What about {THING}?").SetTextVariable("THING", r.Told[slot].RefText));
            return true;
        }

        private bool DoneCondition()
        {
            IntelReport r = _current;
            MBTextManager.SetTextVariable("VI_DONE", r != null && r.Told.Count > 0
                ? new TextObject("{=vi_details_done}That's all I needed to know.")
                : new TextObject("{=vi_details_none}I see. Thank you anyway."));
            return true;
        }

        private bool DetailAnswerCondition()
        {
            IntelReport r = _current;
            if (r == null || _detailIndex < 0 || _detailIndex >= r.Told.Count) return false;
            MBTextManager.SetTextVariable("VI_DETAIL", r.Told[_detailIndex].DetailText);
            return true;
        }

        private void DetailAnswerConsequence()
        {
            try
            {
                IntelReport r = _current;
                if (r == null || _detailIndex < 0 || _detailIndex >= r.Told.Count) return;
                Sighting s = r.Told[_detailIndex];
                if (IntelSettings.DebugMode)
                    DebugChat.Say(s.Lied ? "That was a LIE." : "That was the truth.", s.Lied ? Colors.Red : Colors.Green);
                _lastDetailWasLie = s.Lied && r.LieNoticed;

                if (IntelSettings.RevealHideouts && s.IsHideout && !s.Lied && s.Hideout != null && !s.Hideout.IsSpotted)
                {
                    s.Hideout.IsSpotted = true;
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=vi_hideout_marked}A hideout has been marked on your map.").ToString(), Colors.Green));
                }
            }
            catch (Exception e) { Error(e); }
        }

        // ------------------------------------------------------------------ catching a liar

        // Built on use: DefaultSkills isn't ready yet when the behavior is constructed.
        private static SkillObject[] NoticeSkills => new[] { DefaultSkills.Charm, DefaultSkills.Scouting, DefaultSkills.Roguery };

        /// <summary>
        /// The player notices the lie if their Charm, Scouting or Roguery beats the notable's (by more than the
        /// configured lead). The skill with the biggest lead decides how they notice it.
        /// </summary>
        private static void TryNoticeLie(IntelReport r)
        {
            SkillObject best = null;
            int bestLead = int.MinValue;
            foreach (SkillObject skill in NoticeSkills)
            {
                if (skill == null) continue;
                int lead = Hero.MainHero.GetSkillValue(skill) - r.Notable.GetSkillValue(skill);
                if (lead > bestLead) { bestLead = lead; best = skill; }
            }
            if (IntelSettings.DebugMode)
                DebugChat.Say($"Noticing the lie: best lead {bestLead} in {best?.Name} (needs more than {IntelSettings.NoticeSkillLead}).", Colors.Magenta);
            if (best == null || bestLead <= IntelSettings.NoticeSkillLead) return;

            r.LieNoticed = true;
            r.NoticeSkill = best;
            r.NoticeLead = bestLead;
            Hero.MainHero.AddSkillXp(best, 15f);
        }

        /// <summary>
        /// The "you're lying" reply. Its wording is the flavor: it says how the player saw through the notable,
        /// based on the skill with the biggest lead, or calls out the follow-up answer that was just given.
        /// </summary>
        private bool PressCondition()
        {
            IntelReport r = _current;
            if (r == null || !r.LieNoticed || r.Pressed) return false;

            TextObject line;
            if (_lastDetailWasLie)
                line = new TextObject("{=vi_press_again}There it is again. You can't even keep your story straight. Enough lies. The truth, now.");
            else if (r.NoticeSkill == DefaultSkills.Charm)
                line = new TextObject("{=vi_press_charm}You won't even meet my eye, and that smile came a moment too late. You're holding something back. The truth, now.");
            else if (r.NoticeSkill == DefaultSkills.Scouting)
                line = new TextObject("{=vi_press_scouting}That's not what the tracks on these roads tell me. I know this country. Don't lie to me. The truth, now.");
            else
                line = new TextObject("{=vi_press_roguery}That came out a little too smooth, like a story you'd rehearsed. I know a lie when I hear one. The truth, now.");
            MBTextManager.SetTextVariable("VI_PRESS_LINE", line);
            return true;
        }

        /// <summary>Call the notable a liar. Your skill lead and their liking for you decide whether they cave.</summary>
        private void Press()
        {
            IntelReport r = _current;
            if (r == null) return;
            try
            {
                r.Pressed = true;
                int rel = r.Notable.GetRelation(Hero.MainHero);
                float chance = IntelSettings.PressBaseChance + (r.NoticeLead + rel) * IntelSettings.PressChancePerPoint;
                chance = Math.Max(0.05f, Math.Min(0.95f, chance));
                _pressSucceeded = MBRandom.RandomFloat < chance;
                if (IntelSettings.DebugMode)
                    DebugChat.Say($"Pressing: {chance:0%} chance (lead {r.NoticeLead}, relation {rel}) -> {(_pressSucceeded ? "they confess" : "they refuse")}.", Colors.Magenta);

                if (_pressSucceeded)
                {
                    r.Confess();
                    if (r.NoticeSkill != null) Hero.MainHero.AddSkillXp(r.NoticeSkill, 25f);
                    r.PrintDebug(false);
                }
                else if (IntelSettings.PressFailRelation > 0)
                {
                    ChangeRelationAction.ApplyPlayerRelation(r.Notable, -IntelSettings.PressFailRelation, false, true);
                }
            }
            catch (Exception e) { Error(e); }
        }

        private bool PressSucceededCondition()
        {
            IntelReport r = _current;
            if (r == null || !_pressSucceeded) return false;
            MBTextManager.SetTextVariable("VI_PRESS_OK",
                new TextObject("{=vi_press_ok}...Keep your voice down! Fine. I didn't want any trouble, that's all. Here's how it really is. {SUMMARY}")
                    .SetTextVariable("SUMMARY", r.SummaryText));
            return true;
        }

        private bool PressFailedCondition()
        {
            if (_current == null) return false;
            MBTextManager.SetTextVariable("VI_PRESS_FAIL", MBRandom.RandomFloat < 0.5f
                ? new TextObject("{=vi_press_fail_1}You'd call me a liar in my own village? I've told you what I know. Take it or leave it.")
                : new TextObject("{=vi_press_fail_2}I don't care for your tone. That's all you'll get from me."));
            return true;
        }

        private static void Error(Exception e)
        {
            InformationManager.DisplayMessage(new InformationMessage("[Village Intel] " + e.Message, Colors.Red));
        }
    }
}
