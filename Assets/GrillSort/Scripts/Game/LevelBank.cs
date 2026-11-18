using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace GrillSort
{
    /// <summary>
    /// Baked, solver-verified levels ("Grill Sort > Bake And Verify Levels"). Past the last baked level the same
    /// deterministic generator keeps going at runtime, on a worker thread (<see cref="Prefetch"/>) so it doesn't hitch.
    /// </summary>
    [CreateAssetMenu(menuName = "Grill Sort/Level Bank")]
    public sealed class LevelBank : ScriptableObject
    {
        // device budgets per candidate: exact A* nodes, then the weighted fallback (a level is always solver-verified)
        const int RuntimeCandidates = 8, RuntimeExactBudget = 200000, RuntimeFallbackBudget = 60000;

        public MoveRule rule = MoveRule.AnyWithAbove;
        public List<LevelData> levels = new List<LevelData>();

        readonly Dictionary<int, LevelData> _runtime = new Dictionary<int, LevelData>();
        readonly Dictionary<int, Task<LevelData>> _pending = new Dictionary<int, Task<LevelData>>();

        public int BakedCount => levels.Count;

        static bool Compatible(MoveRule baked, MoveRule active) =>
            baked == active || (baked == MoveRule.TopGroup && active == MoveRule.SingleTop);

        bool IsBaked(int number, MoveRule activeRule) =>
            number >= 1 && number <= levels.Count && Compatible(levels[number - 1].rule, activeRule);

        int PrevPar(int number) => number - 1 >= 1 && number - 1 <= levels.Count ? levels[number - 2].par : 0;

        LevelData Generate(int number, MoveRule activeRule) =>
            LevelGenerator.Generate(number, PrevPar(number), activeRule, RuntimeCandidates, RuntimeExactBudget, RuntimeFallbackBudget);

        /// <summary>Starts generating a runtime level in the background (call it for the next level while this one is played).</summary>
        public void Prefetch(int number, MoveRule activeRule)
        {
            if (number < 1 || IsBaked(number, activeRule)) return;
            if (_runtime.TryGetValue(number, out var done) && done.rule == activeRule) return;
            if (_pending.ContainsKey(number)) return;
            _pending[number] = Task.Run(() => Generate(number, activeRule));
        }

        public LevelData Get(int number, MoveRule activeRule)
        {
            // a TopGroup solution is also a SingleTop solution (a group move = several single moves)
            if (IsBaked(number, activeRule)) return levels[number - 1];
            if (_runtime.TryGetValue(number, out var lv) && lv.rule == activeRule) return lv;
            lv = null;
            if (_pending.TryGetValue(number, out var task))
            {
                _pending.Remove(number);
                try { lv = task.Result; } // waits only if the worker hasn't finished yet
                catch (System.Exception e) { Debug.LogException(e); }
                if (lv != null && lv.rule != activeRule) lv = null;
            }
            if (lv == null) lv = Generate(number, activeRule);
            _runtime[number] = lv;
            return lv;
        }
    }
}
