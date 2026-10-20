using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Procedural animation for rigged placeholder characters: idle breathing, distance-based walk cycle (feet never
    /// skate), five attack styles, hit reaction, stun wobble, wing flapping and a death animation. Everything is driven
    /// by code, so primitive-built characters animate without any animation clips.
    ///
    /// Rig: assign the pivots you have (body, head, armL, armR, legL, legR). Limbs must hang DOWN from their pivot.
    /// Any pivot may be left empty. If you later import a real model, assign an Animator instead: this component
    /// will also drive its "Speed" (float) and "Attack" / "Hit" (triggers) / "Dead" (bool) parameters if they exist.
    /// Put this component on the "Visual" child of the unit prefab.
    /// </summary>
    public class UnitAnimator : MonoBehaviour
    {
        public enum AttackStyle { Swing, Thrust, Shoot, Cast, Slam }

        [Header("Rig pivots (all optional)")]
        [SerializeField] Transform body;
        [SerializeField] Transform head;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;

        [Header("Style")]
        [SerializeField] AttackStyle attackStyle = AttackStyle.Swing;
        [SerializeField] float walkSwing = 38f;
        [SerializeField] float bobHeight = 0.07f;
        [Tooltip("Walk-cycle strides per world unit travelled.")]
        [SerializeField] float stridesPerMeter = 0.9f;
        [SerializeField] float attackDuration = 0.32f;
        [Tooltip("Treat armL/armR as wings and flap them (flying units).")]
        [SerializeField] bool flapWings;
        [SerializeField] float flapSpeed = 14f;
        [SerializeField] float flapAngle = 35f;

        [Header("Optional Animator (imported models)")]
        [SerializeField] Animator animator;

        Unit unit;
        Vector3 lastPos, bodyBasePos;
        Quaternion bodyBaseRot = Quaternion.identity, headBaseRot = Quaternion.identity;
        Quaternion armLBase = Quaternion.identity, armRBase = Quaternion.identity;
        Quaternion legLBase = Quaternion.identity, legRBase = Quaternion.identity;
        float phase, moveBlend, attackT = 1f, hitKick, timeOffset;
        bool dead;
        HashSet<string> animParams;

        static Quaternion BaseOf(Transform t) { return t != null ? t.localRotation : Quaternion.identity; }

        void Awake()
        {
            unit = GetComponentInParent<Unit>();
            if (unit == null) { enabled = false; return; }

            bodyBasePos = body != null ? body.localPosition : Vector3.zero;
            bodyBaseRot = BaseOf(body);
            headBaseRot = BaseOf(head);
            armLBase = BaseOf(armL);
            armRBase = BaseOf(armR);
            legLBase = BaseOf(legL);
            legRBase = BaseOf(legR);
            timeOffset = Random.value * 6.28f;
            lastPos = unit.transform.position;

            unit.AttackPerformed += OnAttack;
            unit.Damaged += OnHit;

            if (animator != null)
            {
                animParams = new HashSet<string>();
                foreach (AnimatorControllerParameter p in animator.parameters) animParams.Add(p.name);
            }
        }

        void OnDestroy()
        {
            if (unit == null) return;
            unit.AttackPerformed -= OnAttack;
            unit.Damaged -= OnHit;
        }

        void OnAttack(Damageable target)
        {
            attackT = 0f;
            Trigger("Attack");
        }

        void OnHit(float amount)
        {
            hitKick = 1f;
            Trigger("Hit");
        }

        void Trigger(string name)
        {
            if (animator != null && animParams != null && animParams.Contains(name)) animator.SetTrigger(name);
        }

        void LateUpdate()
        {
            if (dead || unit == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // --- movement measured from real displacement, so the walk cycle always matches the ground speed ---
            Vector3 p = unit.transform.position;
            Vector3 delta = p - lastPos;
            delta.y = 0f;
            lastPos = p;
            float dist = delta.magnitude;
            moveBlend = Mathf.Lerp(moveBlend, (dist / dt) > 0.15f ? 1f : 0f, 1f - Mathf.Exp(-12f * dt));
            float stride = unit.IsAbilityActive ? 1.6f : 1f; // e.g. charging
            phase += dist * stridesPerMeter * 2f * Mathf.PI * stride;
            hitKick = Mathf.Max(0f, hitKick - dt * 6f);

            float swing = Mathf.Sin(phase) * walkSwing * moveBlend;
            float idle = Mathf.Sin(Time.time * 2.2f + timeOffset);
            bool stunned = unit.IsStunned;

            if (legL != null) legL.localRotation = legLBase * Quaternion.Euler(swing, 0f, 0f);
            if (legR != null) legR.localRotation = legRBase * Quaternion.Euler(-swing, 0f, 0f);

            if (flapWings)
            {
                float a = Mathf.Sin(Time.time * flapSpeed + timeOffset) * flapAngle;
                if (armL != null) armL.localRotation = armLBase * Quaternion.Euler(0f, 0f, -a);
                if (armR != null) armR.localRotation = armRBase * Quaternion.Euler(0f, 0f, a);
            }
            else
            {
                if (armL != null) armL.localRotation = armLBase * Quaternion.Euler(-swing * 0.8f, 0f, 0f);
                if (armR != null) armR.localRotation = armRBase * Quaternion.Euler(swing * 0.8f, 0f, 0f);
            }

            if (body != null)
            {
                float y = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * moveBlend + idle * 0.02f * (1f - moveBlend);
                body.localPosition = bodyBasePos + new Vector3(0f, y, -hitKick * 0.12f);
                body.localRotation = bodyBaseRot * Quaternion.Euler(moveBlend * 5f - hitKick * 12f, 0f, 0f);
            }
            if (head != null)
            {
                head.localRotation = stunned
                    ? headBaseRot * Quaternion.Euler(0f, Mathf.Sin(Time.time * 20f) * 25f, Mathf.Sin(Time.time * 14f) * 15f)
                    : headBaseRot * Quaternion.Euler(-idle * 2f, 0f, 0f);
            }

            if (attackT < 1f)
            {
                attackT = Mathf.Min(1f, attackT + dt / Mathf.Max(0.05f, attackDuration));
                ApplyAttackPose(attackT);
            }

            if (animator != null && animParams != null && animParams.Contains("Speed")) animator.SetFloat("Speed", moveBlend);
        }

        // ---------- Attack poses (k runs 0 -> 1 over attackDuration) ----------

        void ApplyAttackPose(float k)
        {
            float e = Mathf.Sin(k * Mathf.PI); // 0 -> 1 -> 0
            if (flapWings) { Lunge(0.5f * e); return; }

            switch (attackStyle)
            {
                case AttackStyle.Swing:
                    SetArm(armR, armRBase, SwingAngle(k));
                    Lunge(0.12f * e);
                    break;

                case AttackStyle.Thrust:
                    SetArm(armR, armRBase, -90f * e);
                    Lunge(0.4f * e);
                    break;

                case AttackStyle.Shoot:
                    {
                        float r = Mathf.Clamp01(Mathf.Min(k / 0.2f, (1f - k) / 0.3f));
                        SetArm(armL, armLBase, -85f * r);
                        SetArm(armR, armRBase, -80f * r);
                        Lunge(-0.1f * Mathf.Clamp01(1f - Mathf.Abs(k - 0.35f) * 5f)); // recoil as the arrow leaves
                        break;
                    }

                case AttackStyle.Cast:
                    {
                        float r = Mathf.Clamp01(Mathf.Min(k / 0.3f, (1f - k) / 0.3f));
                        SetArm(armL, armLBase, -150f * r);
                        SetArm(armR, armRBase, -150f * r);
                        if (body != null) body.localRotation *= Quaternion.Euler(-14f * r, 0f, 0f);
                        if (head != null) head.localRotation = headBaseRot * Quaternion.Euler(-20f * r, 0f, 0f);
                        break;
                    }

                case AttackStyle.Slam:
                    {
                        float a = k < 0.4f ? Mathf.Lerp(0f, -165f, k / 0.4f)
                                : (k < 0.55f ? Mathf.Lerp(-165f, 25f, (k - 0.4f) / 0.15f) : Mathf.Lerp(25f, 0f, (k - 0.55f) / 0.45f));
                        SetArm(armL, armLBase, a);
                        SetArm(armR, armRBase, a);
                        Lunge(0.15f * e);
                        if (body != null && k > 0.45f && k < 0.7f) body.localPosition -= new Vector3(0f, 0.12f, 0f);
                        break;
                    }
            }
        }

        static float SwingAngle(float k)
        {
            if (k < 0.35f) return Mathf.Lerp(0f, -150f, k / 0.35f);            // raise
            if (k < 0.5f) return Mathf.Lerp(-150f, 15f, (k - 0.35f) / 0.15f);  // chop
            return Mathf.Lerp(15f, 0f, (k - 0.5f) / 0.5f);                     // recover
        }

        static void SetArm(Transform arm, Quaternion baseRot, float degrees)
        {
            if (arm != null) arm.localRotation = baseRot * Quaternion.Euler(degrees, 0f, 0f);
        }

        void Lunge(float amount)
        {
            if (body != null) body.localPosition += new Vector3(0f, 0f, amount);
        }

        // ---------- Death ----------

        /// <summary>Plays the death animation on the unit's visual root, then returns so the Unit can destroy itself.</summary>
        public IEnumerator PlayDeath(Transform v)
        {
            dead = true;
            if (animator != null && animParams != null && animParams.Contains("Dead")) animator.SetBool("Dead", true);

            Vector3 startPos = v.localPosition;
            Quaternion startRot = v.rotation;
            Vector3 startScale = v.localScale;
            Vector3 axis = v.right;
            bool flying = unit != null && unit.IsFlying;
            float fall = flying ? unit.transform.position.y : 0f;
            float dur = 0.55f, t = 0f;

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (flying)
                {
                    v.localPosition = startPos + new Vector3(0f, -fall * k * k, 0f);
                    v.rotation = Quaternion.AngleAxis(720f * k, new Vector3(0f, 1f, 0f)) * Quaternion.AngleAxis(-40f * k, axis) * startRot;
                }
                else
                {
                    v.rotation = Quaternion.AngleAxis(-90f * Mathf.Min(1f, k * 3f), axis) * startRot; // topple backwards
                }
                if (k > 0.7f) v.localScale = startScale * (1f - (k - 0.7f) / 0.3f);
                yield return null;
            }
        }
    }
}
