namespace AlienInvasion.Core
{
    /// <summary>
    /// 1回の襲来イベントの進行状態。
    /// Idle→Descending→Bombarding→Ascending→TripodDeploy→TripodsActive→Departing→Done→Idle の一方向循環。
    /// Ascending は爆撃後に滞留高度へ上昇するフェーズ(母船は消えずに上空で滞留する)。
    /// Departing はトライポッド消滅後に母船が出現高度まで上昇して離脱・消滅するフェーズ。
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

    /// <summary>InvasionState の許可された遷移のみを通す状態機械ロジック。</summary>
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
