using UnityEngine;

namespace CardBattle
{
    /// <summary>When the unit dies it splits into smaller units (free of charge). The minion is another CardData.</summary>
    public class SpawnOnDeathAbility : UnitAbility
    {
        [SerializeField] CardData minionCard;
        [SerializeField] int count = 2;

        public override void OnOwnerDeath()
        {
            if (minionCard == null || CardManager.Instance == null || !MatchManager.IsRunning) return;
            Vector3 pos = Owner.transform.position;
            for (int i = 0; i < count; i++)
            {
                float a = (Mathf.PI * 2f / Mathf.Max(1, count)) * i + 0.6f;
                Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (Owner.BodyRadius + 0.4f);
                CardManager.Instance.SpawnUnitFree(Owner.Team, minionCard, pos + offset);
            }
            Vfx.Dust(pos);
        }
    }
}
