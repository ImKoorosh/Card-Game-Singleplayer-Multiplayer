using UnityEngine;

namespace CardBattle
{
    /// <summary>After running far enough without attacking: faster movement and a heavy first strike.</summary>
    public class ChargeAbility : UnitAbility
    {
        [SerializeField] float chargeDistance = 6f;
        [SerializeField] float speedBoost = 1.9f;
        [SerializeField] float damageMultiplier = 2f;

        float traveled, dustTimer;
        bool announced;

        public bool Charging { get { return traveled >= chargeDistance; } }
        public override float SpeedMultiplier { get { return Charging ? speedBoost : 1f; } }
        public override bool IsActive { get { return Charging; } }

        public override void OnMoved(float distance)
        {
            traveled += distance;
            if (!Charging) return;
            if (!announced) { announced = true; AudioManager.Play(SfxId.Whoosh, 0.7f); }
            dustTimer -= distance;
            if (dustTimer <= 0f) { dustTimer = 1.1f; Vfx.Dust(Owner.transform.position); }
        }

        public override float ModifyOutgoingDamage(Damageable target, float damage)
        {
            return Charging ? damage * damageMultiplier : damage;
        }

        public override void OnAttack(Damageable target, float damage)
        {
            if (Charging)
            {
                Vfx.Sparks(target.AimPoint, new Color(1f, 0.9f, 0.4f));
                AudioManager.Play(SfxId.Deploy, 0.7f);
                if (BattleCameraController.Instance != null) BattleCameraController.Instance.Shake(0.12f, 0.15f);
            }
            traveled = 0f;
            announced = false;
        }
    }
}
