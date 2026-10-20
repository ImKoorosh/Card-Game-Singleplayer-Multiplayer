using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Side or main tower. It is a DefensiveBuilding whose stats are set in the Inspector (no card).
    /// Destroying the main tower ends the match (handled by MatchManager listening to Died).
    /// </summary>
    public class Tower : DefensiveBuilding
    {
        public static readonly List<Tower> Registry = new List<Tower>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Registry.Clear(); }

        [Header("Tower")]
        [SerializeField] bool isMainTower;

        public bool IsMainTower { get { return isMainTower; } }
        public override EntityKind Kind { get { return EntityKind.Tower; } }

        protected override void Start()
        {
            base.Start();
            Damaged += amount => AudioManager.Play(SfxId.TowerHit, 0.55f);
        }

        protected override void OnDeath()
        {
            Vector3 p = transform.position;
            Vfx.Explosion(p + new Vector3(0f, 1.2f, 0f), isMainTower ? 4f : 3f, new Color(1f, 0.6f, 0.2f));
            Vfx.Burst(p + new Vector3(0f, 2f, 0f), new Color(0.6f, 0.58f, 0.55f), 30, 5f, 0.5f, 1.0f, 1.5f);
            AudioManager.Play(SfxId.TowerDestroyed, 1f);
            if (BattleCameraController.Instance != null) BattleCameraController.Instance.Shake(isMainTower ? 0.6f : 0.4f, 0.6f);
            base.OnDeath();
        }

        protected override void Awake()
        {
            base.Awake();
            TeamUtil.Tint(transform, team);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Registry.Remove(this);
        }
    }
}
