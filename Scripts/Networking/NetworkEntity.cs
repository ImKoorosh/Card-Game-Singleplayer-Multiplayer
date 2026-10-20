using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Network companion of every unit / building / tower. It lives next to the existing gameplay component
    /// (Unit, Building, Tower) which keeps all of its rules; this class only replicates the results.
    ///
    /// Server: runs the real simulation (existing code) and publishes health, facing and stun.
    /// Client: the gameplay scripts are inert; this class applies the replicated values so health bars, hit flashes,
    /// death animations and animators work exactly as before. Position comes from NetworkTransform (units only).
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class NetworkEntity : NetworkBehaviour
    {
        readonly NetworkVariable<float> health = new NetworkVariable<float>(0f);
        readonly NetworkVariable<int> cardId = new NetworkVariable<int>(-1);
        readonly NetworkVariable<byte> teamValue = new NetworkVariable<byte>(0);
        readonly NetworkVariable<byte> facing = new NetworkVariable<byte>(0);   // yaw in 256 steps
        readonly NetworkVariable<bool> stunned = new NetworkVariable<bool>(false);

        const float YawStep = 360f / 256f;

        Damageable damageable;
        Unit unit;
        Building building;
        Coroutine stunRoutine;
        float stunEnd;

        // ---------------- server ----------------

        /// <summary>Called by the server right after Instantiate, BEFORE NetworkObject.Spawn.</summary>
        public void ServerSetup(int id, Team team)
        {
            cardId.Value = id;
            teamValue.Value = (byte)team;
            Damageable d = GetComponent<Damageable>();
            if (d != null) health.Value = d.Health; // part of the spawn message, so clients start with the right value
        }

        public override void OnNetworkSpawn()
        {
            damageable = GetComponent<Damageable>();
            unit = GetComponent<Unit>();
            building = GetComponent<Building>();

            if (IsServer)
            {
                health.Value = damageable.Health;
                damageable.HealthChanged += OnServerHealthChanged;
                if (unit != null) StartCoroutine(ServerFacingLoop());
            }
            else
            {
                ApplyInitialRemoteState();
                health.OnValueChanged += OnRemoteHealth;
                stunned.OnValueChanged += OnRemoteStun;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (damageable != null) damageable.HealthChanged -= OnServerHealthChanged;
            health.OnValueChanged -= OnRemoteHealth;
            stunned.OnValueChanged -= OnRemoteStun;
        }

        // Only send when the value really changed: damage ticks that round to the same number cost nothing.
        void OnServerHealthChanged(Damageable d)
        {
            if (Mathf.Abs(health.Value - d.Health) > 0.01f) health.Value = d.Health;
        }

        IEnumerator ServerFacingLoop()
        {
            var wait = new WaitForSeconds(0.1f); // 10 Hz is plenty for a rotation; no per-frame traffic
            while (unit != null && !unit.IsDead)
            {
                byte yaw = (byte)(Mathf.RoundToInt(Mathf.Repeat(unit.VisualRoot.eulerAngles.y, 360f) / YawStep) & 255);
                if (yaw != facing.Value) facing.Value = yaw;
                yield return wait;
            }
        }

        /// <summary>Server: marks the unit as stunned for the clients' wobble animation.</summary>
        public void ServerStun(float seconds)
        {
            if (!IsServer || !IsSpawned) return;
            stunEnd = Mathf.Max(stunEnd, Time.time + seconds);
            stunned.Value = true;
            if (stunRoutine == null) stunRoutine = StartCoroutine(StunLoop());
        }

        IEnumerator StunLoop()
        {
            while (Time.time < stunEnd) yield return null;
            stunned.Value = false;
            stunRoutine = null;
        }

        /// <summary>Server: tells clients this entity attacked (animation, sound, projectile visual). Cosmetic only.</summary>
        public void BroadcastAttack(Damageable target)
        {
            if (!IsServer || !IsSpawned || target == null) return;
            NetworkEntity targetEntity = target.GetComponent<NetworkEntity>();
            if (targetEntity == null || !targetEntity.IsSpawned) return;
            AttackFxClientRpc(targetEntity.NetworkObjectId);
        }

        [ClientRpc]
        void AttackFxClientRpc(ulong targetObjectId)
        {
            if (IsServer) return; // the host already played it through the normal gameplay path
            Damageable target = FindDamageable(targetObjectId);
            if (target == null) return;
            if (unit != null) unit.PlayRemoteAttack(target);
            else
            {
                DefensiveBuilding turret = GetComponent<DefensiveBuilding>();
                if (turret != null) turret.PlayRemoteFire(target);
            }
        }

        static Damageable FindDamageable(ulong objectId)
        {
            NetworkObject no;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out no))
                return no.GetComponent<Damageable>();
            return null;
        }

        // ---------------- client ----------------

        void ApplyInitialRemoteState()
        {
            Team team = (Team)teamValue.Value;
            CardData card = CardRegistry.Get(cardId.Value);
            if (card != null)
            {
                if (unit != null) unit.InitializeRemote(card, team);
                else if (building != null) building.InitializeRemote(card, team);
            }
            if (health.Value > 0f) damageable.ApplyNetworkHealth(health.Value, true); // 0 = value not synced yet; never kill on a default
        }

        void OnRemoteHealth(float previous, float current) { damageable.ApplyNetworkHealth(current, false); }
        void OnRemoteStun(bool previous, bool current) { if (unit != null) unit.NetStunned = current; }

        // Remote units turn smoothly toward the replicated heading.
        void Update()
        {
            if (!IsSpawned || IsServer || unit == null || unit.IsDead) return;
            Transform v = unit.VisualRoot;
            Quaternion goal = Quaternion.Euler(0f, facing.Value * YawStep, 0f);
            v.rotation = Quaternion.RotateTowards(v.rotation, goal, 720f * Time.deltaTime);
        }
    }
}
