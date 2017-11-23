using System.Collections.Generic;
using System.Reflection;
using HugsLib;
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;
using Harmony;
using Verse.AI.Group;
// ReSharper disable InconsistentNaming
// ReSharper disable RedundantAssignment

namespace RimWorldDaysMatter
{
    public class DaysMatter : ModBase
    {
        public override string ModIdentifier { get; } = "DaysMatter";

        private MatteredDayStore _store;

        public override void Initialize()
        {
            base.Initialize();
            Log.Message("[DaysMatter] Loaded.");
        }

        public override void WorldLoaded()
        {
            _store = UtilityWorldObjectManager.GetUtilityWorldObject<MatteredDayStore>();
        }

        public override void Tick(int currentTick)
        {
            base.Tick(currentTick);

            int ticks = Find.TickManager.TicksAbs;
            if (ticks % GenDate.TicksPerHour != 0 || Find.VisibleMap == null || _store == null)
                return;

            Vector2 location = Find.WorldGrid.LongLatOf(Find.VisibleMap.Tile);
            Quadrum quadrum = GenDate.Quadrum(ticks, location.x);
            int dayOfQuadrum = GenDate.DayOfQuadrum(ticks, location.x); // zero based
            int hour = GenDate.HourOfDay(ticks, location.x);

            // check settlement
            int startTicks = Find.TickManager.gameStartAbsTick;
            Quadrum settlementQuadrum = GenDate.Quadrum(startTicks, 0);
            int settlementDay = GenDate.DayOfQuadrum(startTicks, 0); // zero based
            int settlementYears = Mathf.RoundToInt(GenDate.YearsPassedFloat);
            if ((hour == 0 || _store.Settlement.Start() == hour) && settlementQuadrum == quadrum && settlementDay == dayOfQuadrum)
            {
                if (hour == 0)
                    Messages.Message("DM.Message.TodaySettlement".Translate(settlementYears), MessageTypeDefOf.PositiveEvent);
                else
                    StartParty("DM.Letter.SettlementParty".Translate(), _store.Settlement == Duration.AllDay);
            }

            // check built in days
            if (hour == 0 || _store.Birthdays.Start() == hour || _store.MarriageAnniversaries.Start() == hour || _store.LoversAnniversaries.Start() == hour)
            {
                Dictionary<Pawn, DirectPawnRelation> handledRelations = new Dictionary<Pawn, DirectPawnRelation>();

                var colonists = Find.VisibleMap.mapPawns.PawnsInFaction(Faction.OfPlayer);
                foreach (var colonist in colonists)
                {
                    if (colonist.Dead || colonist.NonHumanlikeOrWildMan())
                        continue;

                    // check marriage
                    List<DirectPawnRelation> marriageRelations = colonist.relations.DirectRelations.FindAll(x => x.def == PawnRelationDefOf.Spouse);
                    foreach (DirectPawnRelation relation in marriageRelations)
                    {
                        if (handledRelations.ContainsKey(colonist) || handledRelations.ContainsKey(relation.otherPawn))
                            continue;
                        handledRelations.Add(colonist, relation);
                        int startTick = relation.startTicks + startTicks;
                        int startDay = GenDate.DayOfQuadrum(startTick, location.x);
                        Quadrum startQuadrum = GenDate.Quadrum(startTick, location.x);
                        if (startDay == dayOfQuadrum && startQuadrum == quadrum)
                        {
                            if (hour == 0)
                                Messages.Message("DM.Message.TodayMarriageAnniversary".Translate(colonist.NameStringShort, relation.otherPawn.NameStringShort), MessageTypeDefOf.PositiveEvent);
                            else if (_store.MarriageAnniversaries.Start() == hour)
                                StartParty("DM.Letter.MarriageAnniversaryParty".Translate(colonist.NameStringShort, relation.otherPawn.NameStringShort), _store.MarriageAnniversaries == Duration.AllDay);
                        }
                    }

                    // check relationship
                    List<DirectPawnRelation> loverRelations = colonist.relations.DirectRelations.FindAll(x => x.def == PawnRelationDefOf.Lover);
                    foreach (DirectPawnRelation relation in loverRelations)
                    {
                        if (handledRelations.ContainsKey(colonist) || handledRelations.ContainsKey(relation.otherPawn))
                            continue;
                        handledRelations.Add(colonist, relation);
                        int startTick = relation.startTicks + startTicks;
                        int startDay = GenDate.DayOfQuadrum(startTick, location.x);
                        Quadrum startQuadrum = GenDate.Quadrum(startTick, location.x);
                        if (startDay == dayOfQuadrum && startQuadrum == quadrum)
                        {
                            if (hour == 0)
                                Messages.Message("DM.Message.TodayRelationshipAnniversary".Translate(colonist.NameStringShort, relation.otherPawn.NameStringShort), MessageTypeDefOf.PositiveEvent);
                            else if (_store.LoversAnniversaries.Start() == hour)
                                StartParty("DM.Letter.RelationshipAnniversaryParty".Translate(colonist.NameStringShort, relation.otherPawn.NameStringShort), _store.LoversAnniversaries == Duration.AllDay);
                        }
                    }

                    // check birthday
                    long birthdayTick = colonist.ageTracker.BirthAbsTicks;
                    int birthDate = GenDate.DayOfQuadrum(birthdayTick, location.x); // zero based
                    Quadrum birthQuadrum = GenDate.Quadrum(birthdayTick, location.x);
                    int colonistAge = Mathf.RoundToInt(colonist.ageTracker.AgeChronologicalYearsFloat);
                    if (birthDate == dayOfQuadrum && birthQuadrum == quadrum)
                    {
                        if (hour == 0)
                            Messages.Message("DM.Message.TodayBirthday".Translate(colonist.NameStringShort, colonistAge), MessageTypeDefOf.PositiveEvent);
                        else if (_store.Birthdays.Start() == hour)
                            StartParty("DM.Letter.BirthdayParty".Translate(colonist.NameStringShort), _store.Birthdays == Duration.AllDay);
                    }
                }
            }

            // check custom days
            var matchedEvents = _store.MatteredDays.FindAll(x => x.DayOfQuadrum - 1 == dayOfQuadrum && x.Quadrum == quadrum);
            if (matchedEvents.Count == 0)
                return;

            foreach (MatteredDay day in matchedEvents)
            {
                if (hour == 0)
                    Messages.Message("DM.Message.TodayCustomDay".Translate(day.Name), MessageTypeDefOf.PositiveEvent);
                else if (day.Duration.Start() == hour)
                    StartParty("DM.Letter.CustomDayParty".Translate(day.Name), day.Duration == Duration.AllDay);
            }
        }

        private void StartParty(string reason, bool wholeDay = false, Pawn starter = null)
        {
            TryStartParty(reason, wholeDay, starter);
        }

        private bool TryStartParty(string reason, bool wholeDay, Pawn starter)
        {
            Map currentMap = Find.VisibleMap;

            if (currentMap == null)
                return false;

            if (starter == null)
            {
                starter = PartyUtility.FindRandomPartyOrganizer(Faction.OfPlayer, currentMap);
                if (starter == null)
                {
                    Messages.Message("DM.Error.NoStarter".Translate(), MessageTypeDefOf.NegativeEvent);
                    return false;
                }
            }
            
            IntVec3 intVec;
            if (!RCellFinder.TryFindPartySpot(starter, out intVec))
            {
                Messages.Message("DM.Error.NoSpot".Translate(), MessageTypeDefOf.NegativeEvent);
                return false;
            }

            LordJob partyJob = wholeDay ? new LongJoinableParty(intVec) : new LordJob_Joinable_Party(intVec, starter);
            LordMaker.MakeNewLord(starter.Faction, partyJob, currentMap);
            
            Find.LetterStack.ReceiveLetter("DM.Letter.PartyTitle".Translate(), "DM.Letter.Party".Translate(reason), LetterDefOf.PositiveEvent, new TargetInfo(intVec, currentMap));
            return true;
        }

        [HarmonyPatch(typeof(GenDate), "Quadrum")]
        public static class GenDateQuadrumPatch
        {
            [HarmonyPostfix]
            public static void FixQuadrum(ref Quadrum __result, long absTicks, float longitude)
            {
                var offset = (absTicks / 2500f / 24f / 15f) % 4;
                if (offset < 0)
                    offset = (offset + 4) % 4;
                __result = (Quadrum)Mathf.FloorToInt(offset);
            }
        }

        [HarmonyPatch(typeof(GenDate), "DayOfQuadrum")]
        public static class GenDateDayOfQuadrumPatch
        {
            [HarmonyPostfix]
            public static void FixDayOfQuadrum(ref int __result, long absTicks, float longitude)
            {
                var offset = (absTicks / 2500f / 24f) % 15;
                if (offset < 0)
                    offset = (offset + 15) % 15;
                __result = Mathf.FloorToInt(offset);
            }
        }

        [HarmonyPatch(typeof(Pawn_AgeTracker), "ExposeData")]
        public static class PawnAgeTrackerExposeDataPatch
        {
            private static readonly FieldInfo AGE_BIOLOGICAL_TICKS_INT_FIELD = AccessTools.Field(typeof(Pawn_AgeTracker), "ageBiologicalTicksInt");
            private static readonly FieldInfo BIRTH_ABS_TICKS_INT_FIELD = AccessTools.Field(typeof(Pawn_AgeTracker), "birthAbsTicksInt");

            [HarmonyPrefix]
            public static void PreFix(Pawn_AgeTracker __instance)
            {
                long birthAbsTicksInt = (long)BIRTH_ABS_TICKS_INT_FIELD.GetValue(__instance);
                long ageBiologicalTicksInt = (long)AGE_BIOLOGICAL_TICKS_INT_FIELD.GetValue(__instance);

                if (birthAbsTicksInt < 0 && Scribe.mode == LoadSaveMode.Saving)
                {
                    AGE_BIOLOGICAL_TICKS_INT_FIELD.SetValue(__instance, ageBiologicalTicksInt - Find.TickManager.gameStartAbsTick);
                }
            }

            [HarmonyPostfix]
            public static void PostFix(Pawn_AgeTracker __instance)
            {
                long birthAbsTicksInt = (long)BIRTH_ABS_TICKS_INT_FIELD.GetValue(__instance);
                long ageBiologicalTicksInt = (long)AGE_BIOLOGICAL_TICKS_INT_FIELD.GetValue(__instance);

                if (birthAbsTicksInt < 0 && (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.PostLoadInit))
                {
                    AGE_BIOLOGICAL_TICKS_INT_FIELD.SetValue(__instance, ageBiologicalTicksInt + Find.TickManager.gameStartAbsTick);
                }
            }
        }

        [HarmonyPatch(typeof(Pawn_AgeTracker))]
        [HarmonyPatch("BirthDayOfSeasonZeroBased", PropertyMethod.Getter)]
        public static class PawnAgeTrackerBirthDayOfSeasonZeroBasedPatch
        {
            private static readonly FieldInfo BIRTH_ABS_TICKS_INT_FIELD = AccessTools.Field(typeof(Pawn_AgeTracker), "birthAbsTicksInt");

            [HarmonyPostfix]
            public static void Fix(Pawn_AgeTracker __instance, ref int __result)
            {
                long birthAbsTicksInt = (long)BIRTH_ABS_TICKS_INT_FIELD.GetValue(__instance);
                __result = GenDate.DayOfQuadrum(birthAbsTicksInt, 0f);
            }
        }
    }
}