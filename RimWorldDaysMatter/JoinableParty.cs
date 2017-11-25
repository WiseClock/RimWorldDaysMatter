using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimWorldDaysMatter
{
    public class JoinableParty : LordJob_Joinable_Party
    {
        private readonly List<Pawn> _invited;

        public JoinableParty(IntVec3 spot, Pawn starter, List<Pawn> invited = null)
            : base(spot, starter)
        {
            _invited = invited;
        }

        public override float VoluntaryJoinPriorityFor(Pawn p)
        {
            if (_invited != null && !_invited.Contains(p))
                return 0f;
            return base.VoluntaryJoinPriorityFor(p);
        }
    }
}