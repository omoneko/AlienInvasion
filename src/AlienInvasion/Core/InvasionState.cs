namespace AlienInvasion.Core
{
    /// <summary>
    /// How far one invasion has progressed. The states form a one-way cycle:
    /// Idle, Descending, Bombarding, Ascending, TripodDeploy, TripodsActive, Departing, Done,
    /// and back to Idle.
    /// Ascending is the climb to the loitering altitude after the bombardment - the mothership
    /// does not leave, it waits overhead.
    /// Departing is the climb back to the spawn altitude once the tripods are gone, after which
    /// the mothership leaves and disappears.
    /// </summary>
    public enum InvasionState
    {
        Idle,
        Descending,
        Bombarding,
        Ascending,
        TripodDeploy,
        TripodsActive,
        Departing,
        Done
    }

    /// <summary>State machine that permits only the legal InvasionState transitions.</summary>
    public static class InvasionStateMachine
    {
        public static bool CanTransition(InvasionState from, InvasionState to)
        {
            return Next(from) == to;
        }

        public static InvasionState Next(InvasionState current)
        {
            switch (current)
            {
                case InvasionState.Idle: return InvasionState.Descending;
                case InvasionState.Descending: return InvasionState.Bombarding;
                case InvasionState.Bombarding: return InvasionState.Ascending;
                case InvasionState.Ascending: return InvasionState.TripodDeploy;
                case InvasionState.TripodDeploy: return InvasionState.TripodsActive;
                case InvasionState.TripodsActive: return InvasionState.Departing;
                case InvasionState.Departing: return InvasionState.Done;
                case InvasionState.Done: return InvasionState.Idle;
                default: return InvasionState.Idle;
            }
        }
    }
}
