using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Arena geometry: size, river, bridges, tower slots, deploy rules and ground pathing.
    /// Player side is -Z, enemy side is +Z. The river is the band |z| &lt; riverWidth/2, crossable only on bridges.
    /// Also builds simple placeholder visuals at runtime (turn off "Build Visuals" to use your own art).
    /// </summary>
    public class ArenaLayout : MonoBehaviour
    {
        public static ArenaLayout Instance { get; private set; }

        [Header("Dimensions (world units)")]
        [SerializeField] float width = 18f;
        [SerializeField] float length = 32f;
        [SerializeField] float riverWidth = 2.4f;
        [SerializeField] float bridgeWidth = 3.2f;
        [SerializeField] float[] bridgeXs = { -4.5f, 4.5f };

        [Header("Tower slots (distance from the centre line)")]
        [SerializeField] float sideTowerX = 5.2f;
        [SerializeField] float sideTowerDistance = 8.4f;
        [SerializeField] float mainTowerDistance = 12.7f;

        [Header("Deployment")]
        [SerializeField] float deployMargin = 0.6f;

        [Header("Placeholder visuals")]
        [SerializeField] bool buildVisuals = true;

        public float Width { get { return width; } }
        public float Length { get { return length; } }
        public float HalfWidth { get { return width * 0.5f; } }
        public float HalfLength { get { return length * 0.5f; } }
        public float RiverHalf { get { return riverWidth * 0.5f; } }
        public IList<float> BridgeXs { get { return bridgeXs; } }

        void Awake()
        {
            Instance = this;
            if (buildVisuals && transform.Find("ArenaVisuals") == null) BuildVisuals();
        }

        /// <summary>slot 0 = left side tower, 1 = main tower, 2 = right side tower.</summary>
        public Vector3 TowerSlot(Team team, int slot)
        {
            float dir = team == Team.Player ? -1f : 1f;
            if (slot == 1) return new Vector3(0f, 0f, dir * mainTowerDistance);
            return new Vector3(slot == 0 ? -sideTowerX : sideTowerX, 0f, dir * sideTowerDistance);
        }

        public Vector3 ClampToArena(Vector3 p, float margin)
        {
            p.x = Mathf.Clamp(p.x, -HalfWidth + margin, HalfWidth - margin);
            p.z = Mathf.Clamp(p.z, -HalfLength + margin, HalfLength - margin);
            p.y = 0f;
            return p;
        }

        public float NearestBridgeX(float x)
        {
            float best = 0f, bestDist = float.MaxValue;
            for (int i = 0; i < bridgeXs.Length; i++)
            {
                float d = Mathf.Abs(bridgeXs[i] - x);
                if (d < bestDist) { bestDist = d; best = bridgeXs[i]; }
            }
            return best;
        }

        // Bridge that minimises the total walking distance from -> bridge -> to.
        float BestBridgeX(float fromX, float toX)
        {
            float best = 0f, bestCost = float.MaxValue;
            for (int i = 0; i < bridgeXs.Length; i++)
            {
                float cost = Mathf.Abs(fromX - bridgeXs[i]) + Mathf.Abs(bridgeXs[i] - toX);
                if (cost < bestCost) { bestCost = cost; best = bridgeXs[i]; }
            }
            return best;
        }

        /// <summary>
        /// Next point a unit should walk to on its way to <paramref name="to"/>.
        /// Flying units go straight; ground units are routed through the nearest useful bridge.
        /// </summary>
        public Vector3 GetNextWaypoint(Vector3 from, Vector3 to, bool flying)
        {
            if (flying) return to;
            float rh = RiverHalf;
            bool fromInBand = Mathf.Abs(from.z) < rh;
            bool toInBand = Mathf.Abs(to.z) < rh;
            float fromSide = from.z >= 0f ? 1f : -1f;
            float toSide = to.z >= 0f ? 1f : -1f;

            if (!fromInBand && !toInBand && fromSide == toSide) return to; // same bank: straight line

            float bx = fromInBand ? NearestBridgeX(from.x) : BestBridgeX(from.x, to.x);
            bool aligned = Mathf.Abs(from.x - bx) <= bridgeWidth * 0.25f;

            if (fromInBand || aligned)
            {
                if (toInBand) return to; // target is standing on the bridge
                return new Vector3(bx, from.y, toSide * (rh + 0.4f)); // walk to the far end of the bridge
            }
            return new Vector3(bx, from.y, fromSide * (rh + 0.3f)); // walk along our bank to the bridge entrance
        }

        /// <summary>
        /// Keeps a ground unit inside the arena and out of the water. If a push moved it into the river
        /// away from a bridge it is returned to the bank it came from (prevents getting stuck/teleporting).
        /// </summary>
        public Vector3 ConstrainGround(Vector3 p, float radius, float previousZ)
        {
            float hx = HalfWidth - radius, hz = HalfLength - radius;
            p.x = Mathf.Clamp(p.x, -hx, hx);
            p.z = Mathf.Clamp(p.z, -hz, hz);
            if (Mathf.Abs(p.z) < RiverHalf)
            {
                float bx = NearestBridgeX(p.x);
                float halfSpan = bridgeWidth * 0.5f - radius * 0.5f;
                if (Mathf.Abs(p.x - bx) > halfSpan) p.z = (previousZ >= 0f ? 1f : -1f) * RiverHalf;
                else p.x = Mathf.Clamp(p.x, bx - halfSpan, bx + halfSpan);
            }
            return p;
        }

        /// <summary>Validates a deployment position. Never throws; explains why in <paramref name="reason"/>.</summary>
        public bool IsValidDeployPoint(Team team, CardData card, Vector3 p, out string reason)
        {
            reason = null;
            if (card == null) { reason = "No card selected"; return false; }
            if (Mathf.Abs(p.x) > HalfWidth - 0.2f || Mathf.Abs(p.z) > HalfLength - 0.2f)
            {
                reason = "Outside the arena";
                return false;
            }
            if (card.cardType == CardType.Spell) return true; // spells may target anywhere

            float minZ = RiverHalf + deployMargin;
            bool ownSide = team == Team.Player ? p.z <= -minZ : p.z >= minZ;
            if (!ownSide) { reason = "Deploy on your own side"; return false; }

            float r = card.stats != null ? card.stats.bodyRadius : 0.5f;
            var all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (d == null || d.Kind == EntityKind.Unit) continue;
                Vector3 diff = d.transform.position - p;
                diff.y = 0f;
                if (diff.magnitude < d.BodyRadius + r * 0.5f) { reason = "Blocked by a building"; return false; }
            }
            return true;
        }

        // ---------- Placeholder visuals ----------

        [ContextMenu("Rebuild Visuals")]
        public void BuildVisuals()
        {
            Transform old = transform.Find("ArenaVisuals");
            if (old != null) PlaceholderUtil.DestroyObject(old.gameObject);

            Transform root = new GameObject("ArenaVisuals").transform;
            root.SetParent(transform, false);

            Color grassA = new Color(0.36f, 0.68f, 0.30f), grassB = new Color(0.31f, 0.62f, 0.27f);
            Material mA = PlaceholderUtil.MakeLit(grassA), mB = PlaceholderUtil.MakeLit(grassB);
            Material water = PlaceholderUtil.MakeLit(new Color(0.20f, 0.50f, 0.85f));
            Material wood = PlaceholderUtil.MakeLit(new Color(0.55f, 0.38f, 0.22f));
            Material stone = PlaceholderUtil.MakeLit(new Color(0.55f, 0.55f, 0.6f));
            Material outside = PlaceholderUtil.MakeLit(new Color(0.22f, 0.45f, 0.24f));

            // Mowed-lawn stripes on each half.
            float rh = RiverHalf, halfLen = HalfLength;
            int stripes = 6;
            float stripeLen = (halfLen - rh) / stripes;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < stripes; i++)
                {
                    float zc = side * (rh + stripeLen * (i + 0.5f));
                    PlaceholderUtil.Part(PrimitiveType.Cube, "Grass", root, new Vector3(0f, -0.1f, zc),
                        new Vector3(width, 0.2f, stripeLen), (i % 2 == 0) ? mA : mB);
                }

            PlaceholderUtil.Part(PrimitiveType.Cube, "River", root, new Vector3(0f, -0.18f, 0f), new Vector3(width, 0.2f, riverWidth), water);
            for (int i = 0; i < bridgeXs.Length; i++)
                PlaceholderUtil.Part(PrimitiveType.Cube, "Bridge", root, new Vector3(bridgeXs[i], -0.02f, 0f),
                    new Vector3(bridgeWidth, 0.2f, riverWidth + 0.8f), wood);

            // Low walls + a big dark ground plate so the arena reads clearly on small screens.
            float wallH = 0.6f, t = 0.5f;
            PlaceholderUtil.Part(PrimitiveType.Cube, "WallL", root, new Vector3(-HalfWidth - t * 0.5f, wallH * 0.5f - 0.2f, 0f), new Vector3(t, wallH, length + t * 2f), stone);
            PlaceholderUtil.Part(PrimitiveType.Cube, "WallR", root, new Vector3(HalfWidth + t * 0.5f, wallH * 0.5f - 0.2f, 0f), new Vector3(t, wallH, length + t * 2f), stone);
            PlaceholderUtil.Part(PrimitiveType.Cube, "WallN", root, new Vector3(0f, wallH * 0.5f - 0.2f, HalfLength + t * 0.5f), new Vector3(width, wallH, t), stone);
            PlaceholderUtil.Part(PrimitiveType.Cube, "WallS", root, new Vector3(0f, wallH * 0.5f - 0.2f, -HalfLength - t * 0.5f), new Vector3(width, wallH, t), stone);
            PlaceholderUtil.Part(PrimitiveType.Cube, "Outside", root, new Vector3(0f, -0.5f, 0f), new Vector3(width + 14f, 0.2f, length + 14f), outside);

            // Stylised trees around the edge (deterministic pattern, no randomness).
            Material trunk = PlaceholderUtil.MakeLit(new Color(0.4f, 0.27f, 0.15f));
            Material leaves = PlaceholderUtil.MakeLit(new Color(0.16f, 0.5f, 0.22f));
            for (int i = 0; i < 7; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = -HalfLength + 2f + i * ((length - 4f) / 6f);
                    float x = s * (HalfWidth + 1.8f + (i % 2) * 0.9f);
                    PlaceholderUtil.Part(PrimitiveType.Cylinder, "Trunk", root, new Vector3(x, 0.5f, z), new Vector3(0.35f, 0.6f, 0.35f), trunk);
                    PlaceholderUtil.Part(PrimitiveType.Sphere, "Leaves", root, new Vector3(x, 1.7f, z), new Vector3(1.6f, 1.6f, 1.6f), leaves);
                }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, 0.1f, length));
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(width, 0.05f, riverWidth));
            Gizmos.color = new Color(0.8f, 0.6f, 0.3f, 0.8f);
            if (bridgeXs != null)
                for (int i = 0; i < bridgeXs.Length; i++)
                    Gizmos.DrawCube(new Vector3(bridgeXs[i], 0f, 0f), new Vector3(bridgeWidth, 0.08f, riverWidth + 0.8f));
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.15f);
            Gizmos.DrawCube(new Vector3(0f, 0f, -(HalfLength + RiverHalf + deployMargin) * 0.5f), new Vector3(width, 0.05f, HalfLength - RiverHalf - deployMargin));
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.15f);
            Gizmos.DrawCube(new Vector3(0f, 0f, (HalfLength + RiverHalf + deployMargin) * 0.5f), new Vector3(width, 0.05f, HalfLength - RiverHalf - deployMargin));
        }
    }
}
