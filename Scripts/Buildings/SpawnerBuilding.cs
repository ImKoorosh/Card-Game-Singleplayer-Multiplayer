using System.Collections;
using UnityEngine;

namespace CardBattle
{
    /// <summary>A building that periodically spawns free units toward the enemy side (a "barracks").</summary>
    public class SpawnerBuilding : Building
    {
        [SerializeField] CardData minionCard;
        [SerializeField] float spawnInterval = 4.5f;
        [SerializeField] float firstDelay = 1.5f;

        protected override void Start()
        {
            base.Start();
            if (NetworkGame.Authority) StartCoroutine(SpawnLoop()); // only the server spawns minions
        }

        IEnumerator SpawnLoop()
        {
            yield return new WaitForSeconds(firstDelay);
            var wait = new WaitForSeconds(spawnInterval);
            while (!IsDead)
            {
                if (MatchManager.IsRunning && minionCard != null && CardManager.Instance != null)
                {
                    Vector3 p = transform.position + new Vector3(0f, 0f, team.ForwardZ() * (bodyRadius + 0.9f));
                    CardManager.Instance.SpawnUnitFree(team, minionCard, p);
                    Vfx.Dust(p);
                    AudioManager.Play(SfxId.Spawn, 0.5f);
                }
                yield return wait;
            }
        }
    }
}
