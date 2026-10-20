using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Base class for per-unit special abilities. Add one or more ability components to a unit prefab and the
    /// Unit will call the hooks below. To create a new ability: derive from this class and override only the hooks you need.
    /// </summary>
    public abstract class UnitAbility : MonoBehaviour
    {
        protected Unit Owner { get; private set; }

        /// <summary>Multiplies the unit's movement speed (e.g. charging).</summary>
        public virtual float SpeedMultiplier { get { return 1f; } }
        /// <summary>True while the ability is visibly "on" (used by UnitAnimator, e.g. faster stride).</summary>
        public virtual bool IsActive { get { return false; } }

        public void Bind(Unit owner)
        {
            Owner = owner;
            OnBind();
        }

        protected virtual void OnBind() { }
        /// <summary>Change the damage of the unit's next attack.</summary>
        public virtual float ModifyOutgoingDamage(Damageable target, float damage) { return damage; }
        /// <summary>Change (e.g. absorb) damage the unit is about to take.</summary>
        public virtual float ModifyIncomingDamage(float damage) { return damage; }
        public virtual void OnMoved(float distance) { }
        /// <summary>Called right after every attack. 'damage' is the damage that attack was computed with.</summary>
        public virtual void OnAttack(Damageable target, float damage) { }
        public virtual void OnOwnerDeath() { }
    }
}
