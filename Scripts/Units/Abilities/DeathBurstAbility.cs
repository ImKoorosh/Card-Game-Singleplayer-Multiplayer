using UnityEngine;

namespace CardBattle
{
    /// <summary>Explodes when the unit dies. With "Explode On Attack" the unit blows itself up on its first hit (suicide bomber).</summary>
    public class DeathBurstAbility : UnitAbility
    {
        [SerializeField] float radius = 2.2f;
        [SerializeField] float damage = 250f;
        [SerializeField] float towerMultiplier = 0.5f;
        [SerializeField] bool explodeOnAttack = false;
        [SerializeField] Color color = new Color(1f, 0.5f, 0.15f);

        public override void OnAttack(Damageable target, float dmg)
        {
            if (explodeOnAttack) Owner.SelfDestruct();
        }

        public override void OnOwnerDeath()
        {
            Vector3 pos = Owner.transform.position;
            CombatUtil.DamageArea(pos, radius, Owner.Team, damage, Owner, true, true, towerMultiplier);
            NetFx.Explosion(pos, radius, color, 0.18f);
            AudioManager.Play(SfxId.Explosion, 0.8f);
        }
    }
}
