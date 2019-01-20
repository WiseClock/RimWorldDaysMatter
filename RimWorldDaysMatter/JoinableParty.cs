using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimWorldDaysMatter
{
    public class JoinableParty : LordJob_Joinable_Party
    {
        private IntVec3 _spot;
        private Trigger_TicksPassed _timeoutTrigger;
        private readonly List<Pawn> _invited;

        public JoinableParty()
        {
            
        }

        public JoinableParty(IntVec3 spot, List<Pawn> invited = null, Pawn organizer = null) : base(spot, organizer)
        {
            _spot = spot;
            _invited = invited;
        }

        private bool ShouldBeCalledOff()
        {
            return !PartyUtility.AcceptableGameConditionsToContinueParty(Map) || (!_spot.Roofed(Map) && !JoyUtility.EnjoyableOutsideNow(Map));
        }

        protected virtual int GetRandomPartyLength()
        {
            return Rand.RangeInclusive(5000, 15000);
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            LordToil_Party lordToilParty = new LordToil_Party(_spot);
            stateGraph.AddToil(lordToilParty);
            LordToil_End lordToilEnd = new LordToil_End();
            stateGraph.AddToil(lordToilEnd);
            Transition transition = new Transition(lordToilParty, lordToilEnd);
            transition.AddTrigger(new Trigger_TickCondition(ShouldBeCalledOff));
            transition.AddTrigger(new Trigger_PawnLostViolently());
            transition.AddPreAction(new TransitionAction_Message("MessagePartyCalledOff".Translate(), MessageTypeDefOf.NegativeEvent, new TargetInfo(_spot, Map)));
            stateGraph.AddTransition(transition);
            _timeoutTrigger = new Trigger_TicksPassed(GetRandomPartyLength());
            Transition transition2 = new Transition(lordToilParty, lordToilEnd);
            transition2.AddTrigger(_timeoutTrigger);
            transition2.AddPreAction(new TransitionAction_Message("MessagePartyFinished".Translate(), MessageTypeDefOf.NegativeEvent, new TargetInfo(_spot, Map)));
            stateGraph.AddTransition(transition2);
            return stateGraph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref _spot, "spot");
        }

        public override float VoluntaryJoinPriorityFor(Pawn p)
        {
            if (!IsInvited(p))
            {
                return 0f;
            }
            if (!PartyUtil.ShouldPawnKeepPartying(p))
            {
                return 0f;
            }
            if (!lord.ownedPawns.Contains(p) && IsPartyAboutToEnd())
            {
                return 0f;
            }
            return VoluntarilyJoinableLordJobJoinPriorities.PartyGuest;
        }

        private bool IsPartyAboutToEnd()
        {
            return _timeoutTrigger.TicksLeft < 1200;
        }

        private bool IsInvited(Pawn p)
        {
            if (!p.IsColonist || !p.RaceProps.Humanlike)
                return false;
            if (_invited == null)
                return p.Faction == lord.faction;
            return _invited.Contains(p);
        }
    }
}
