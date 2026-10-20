using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Ranged/instant attacker that hovers: ignores the river and bridges, only collides with other
    /// flyers, and can be hit only by units/buildings that can target air.
    /// </summary>
    public class FlyingUnit : RangedUnit
    {
        [SerializeField] float hoverHeight = 2.4f;
        [SerializeField] float bobAmplitude = 0.15f;
        [SerializeField] float bobSpeed = 3f;
        float bobPhase;

        public override bool IsFlying { get { return true; } }

        public override void Initialize(CardData card, Team owner)
        {
            base.Initialize(card, owner);
            SnapToHover();
        }

        protected override void Start()
        {
            base.Start();
            SnapToHover();
            bobPhase = Random.value * 6.28f;
        }

        void SnapToHover()
        {
            Vector3 p = transform.position;
            p.y = hoverHeight;
            transform.position = p;
        }

        void LateUpdate()
        {
            if (visual == null || IsDead) return;
            visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobAmplitude, 0f);
        }
    }
}
