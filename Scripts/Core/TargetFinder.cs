using UnityEngine;

namespace CardBattle
{
    /// <summary>Shared target selection used by units, buildings and towers.</summary>
    public static class TargetFinder
    {
        /// <param name="range">Only targets whose edge is within this distance are "in range".</param>
        /// <param name="fallbackToStructures">
        /// If nothing is in range, return the closest enemy building/tower anywhere on the map
        /// (this is what makes units march toward the enemy base).
        /// </param>
        public static Damageable Find(Team myTeam, Vector3 pos, float range, bool canAir, bool canGround,
                                      TargetPriority priority, bool fallbackToStructures)
        {
            Damageable bestUnit = null, bestStruct = null;
            float unitDist = float.MaxValue, structDist = float.MaxValue;
            var all = Damageable.All;

            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (d == null || d.IsDead || d.Team == myTeam) continue;
                if (d.IsFlying ? !canAir : !canGround) continue;

                float e = d.EdgeDistance(pos);
                if (d.Kind == EntityKind.Unit)
                {
                    if (e < unitDist) { unitDist = e; bestUnit = d; }
                }
                else if (e < structDist) { structDist = e; bestStruct = d; }
            }

            Damageable unitInRange = (bestUnit != null && unitDist <= range) ? bestUnit : null;
            Damageable structInRange = (bestStruct != null && structDist <= range) ? bestStruct : null;
            Damageable fallback = fallbackToStructures ? bestStruct : null;

            switch (priority)
            {
                case TargetPriority.BuildingsOnly:
                    return structInRange != null ? structInRange : fallback;

                case TargetPriority.UnitsFirst:
                    if (unitInRange != null) return unitInRange;
                    return structInRange != null ? structInRange : fallback;

                default: // Nearest
                    if (unitInRange != null && structInRange != null) return unitDist <= structDist ? unitInRange : structInRange;
                    if (unitInRange != null) return unitInRange;
                    return structInRange != null ? structInRange : fallback;
            }
        }
    }
}
