using System.Collections.Generic;
using Verse;

namespace RimWorldDaysMatter
{
    public class LongJoinableParty : JoinableParty
    {
        public LongJoinableParty(IntVec3 spot, List<Pawn> invited = null)
            : base(spot, invited)
        {
        }

        public LongJoinableParty() : base()
        {
            
        }

        protected override int GetRandomPartyLength()
        {
            return Rand.RangeInclusive(20000, 30000);
        }
    }
}
