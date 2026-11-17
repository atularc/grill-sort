using System.Collections.Generic;
using UnityEngine;

namespace GrillSort
{
    /// <summary>
    /// Baked, solver-verified levels ("Grill Sort > Bake And Verify Levels"). Past the last baked level the same
    /// deterministic generator keeps going at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Grill Sort/Level Bank")]
    public sealed class LevelBank : ScriptableObject
    {
        public MoveRule rule = MoveRule.TopGroup;
        public List<LevelData> levels = new List<LevelData>();

        readonly Dictionary<int, LevelData> _runtime = new Dictionary<int, LevelData>();

        public int BakedCount => levels.Count;

        static bool Compatible(MoveRule baked, MoveRule active) =>
            baked == active || (baked == MoveRule.TopGroup && active == MoveRule.SingleTop);

        public LevelData Get(int number, MoveRule activeRule)
        {
            // a TopGroup solution is also a SingleTop solution (a group move = several single moves)
            if (number >= 1 && number <= levels.Count && Compatible(levels[number - 1].rule, activeRule)) return levels[number - 1];
            if (_runtime.TryGetValue(number, out var lv) && lv.rule == activeRule) return lv;
            int prevPar = number - 1 >= 1 && number - 1 <= levels.Count ? levels[number - 2].par : Mathf.Max(0, 20 + number / 4);
            // smaller budget on device; still solver-verified
            lv = LevelGenerator.Generate(number, prevPar, activeRule, 6, 60000);
            _runtime[number] = lv;
            return lv;
        }
    }
}
