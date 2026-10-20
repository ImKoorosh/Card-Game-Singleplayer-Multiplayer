using System.Collections;
using UnityEngine;

namespace CardBattle
{
    public enum AuraMode { Heal, Rage }

    /// <summary>
    /// Buffs nearby allies: Heal restores health over time, Rage boosts their speed and damage.
    /// Runs on a coroutine timer (not every frame) and pulses a ring on the ground so the area is readable.
    /// </summary>
    public class AuraAbility : UnitAbility
    {
        [SerializeField] AuraMode mode = AuraMode.Heal;
        [SerializeField] float radius = 3.5f;
        [SerializeField] float interval = 0.5f;
        [Tooltip("Heal mode: health restored per second to each ally.")]
        [SerializeField] float healPerSecond = 45f;
        [Tooltip("Rage mode: multipliers applied to allies in range.")]
        [SerializeField] float speedBonus = 1.3f;
        [SerializeField] float damageBonus = 1.25f;
        [SerializeField] Color color = new Color(0.4f, 1f, 0.5f);

        protected override void OnBind() { StartCoroutine(Pulse()); }

        IEnumerator Pulse()
        {
            var wait = new WaitForSeconds(interval);
            int tick = 0;
            while (Owner != null && !Owner.IsDead)
            {
                yield return wait;
                if (!MatchManager.IsRunning || Owner.IsStunned) continue;

                Vector3 pos = Owner.transform.position;
                var allies = CombatUtil.Collect(pos, radius, d => d.Team == Owner.Team && d != Owner && d.Kind != EntityKind.Tower);
                for (int i = 0; i < allies.Count; i++)
                {
                    if (mode == AuraMode.Heal) allies[i].Heal(healPerSecond * interval);
                    else
                    {
                        Unit u = allies[i] as Unit;
                        if (u != null) u.ApplyBuff(speedBonus, damageBonus, interval + 0.3f);
                    }
                }
                if (allies.Count > 0 && tick % 2 == 0)
                    Vfx.Burst(allies[Random.Range(0, allies.Count)].AimPoint, color, 3, 0.7f, 0.2f, 0.7f, -0.8f);
                if (tick % 4 == 0) Vfx.Ring(pos, radius, color, 0.7f);
                tick++;
            }
        }
    }
}
