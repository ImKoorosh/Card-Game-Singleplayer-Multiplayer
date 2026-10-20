using Unity.Netcode;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// In-scene network object of the Battle scene. It connects the existing MatchManager to the network:
    ///  - server: publishes match state and the timer (NetworkVariables, 1 update per second) and sends the end result;
    ///  - clients: feed those values into their MatchManager so the existing UI, audio and timer code works unchanged;
    ///  - broadcasts purely cosmetic effects (explosions, lightning, spell visuals) to remote clients.
    /// </summary>
    public class NetworkMatchManager : NetworkBehaviour
    {
        public static NetworkMatchManager Instance { get; private set; }

        readonly NetworkVariable<byte> state = new NetworkVariable<byte>(0);
        readonly NetworkVariable<int> seconds = new NetworkVariable<int>(0);
        readonly NetworkVariable<int> graceSeconds = new NetworkVariable<int>(0);   // > 0 while waiting for a disconnected opponent

        /// <summary>Seconds left to wait for a disconnected opponent (0 = not waiting). Read by the connection status overlay.</summary>
        public int GraceSeconds { get { return graceSeconds.Value; } }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            MatchManager mm = MatchManager.Instance;
            if (mm == null) return;

            if (IsServer)
            {
                mm.StateChanged += s => state.Value = (byte)s;
                mm.TimerChanged += sec => seconds.Value = sec;
                mm.DoubleResourceStarted += () => DoubleManaClientRpc();
                mm.MatchEnded += r => MatchEndedClientRpc((byte)mm.Result, mm.EndReason ?? "");
            }
            else
            {
                state.OnValueChanged += (p, c) => { if (MatchManager.Instance != null) MatchManager.Instance.RemoteSetState((MatchState)c); };
                seconds.OnValueChanged += (p, c) => { if (MatchManager.Instance != null) MatchManager.Instance.RemoteTimer(c); };
                mm.RemoteSetState((MatchState)state.Value);   // late join / reconnect: catch up with the current state
                mm.RemoteTimer(seconds.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        public void ServerSetGrace(int secondsLeft)
        {
            if (IsServer && graceSeconds.Value != secondsLeft) graceSeconds.Value = secondsLeft;
        }

        // ---------------- important gameplay events ----------------

        [ClientRpc]
        void MatchEndedClientRpc(byte playerPerspectiveResult, string reason)
        {
            if (IsServer) return;
            if (MatchManager.Instance != null) MatchManager.Instance.RemoteEnd((MatchResult)playerPerspectiveResult, reason);
        }

        [ClientRpc]
        void DoubleManaClientRpc()
        {
            if (IsServer) return;
            if (MatchManager.Instance != null) MatchManager.Instance.RemoteDoubleResource();
        }

        // ---------------- cosmetic effects (host already played them locally) ----------------

        [ClientRpc]
        public void ExplosionClientRpc(Vector3 pos, float radius, Color color, float shake)
        {
            if (IsServer) return;
            Vfx.Explosion(pos, radius, color);
            AudioManager.Play(SfxId.Explosion, 0.8f);
            if (shake > 0f && BattleCameraController.Instance != null) BattleCameraController.Instance.Shake(shake, 0.25f);
        }

        [ClientRpc]
        public void BoltClientRpc(Vector3 from, Vector3 to, Color color)
        {
            if (IsServer) return;
            Vfx.Bolt(from, to, color);
        }

        /// <summary>Remote clients get a visual-only copy of the spell. The server alone applies damage, healing, slow and stun.</summary>
        [ClientRpc]
        public void SpellClientRpc(int cardId, byte team, Vector3 position)
        {
            if (IsServer) return;
            CardData card = CardRegistry.Get(cardId);
            if (card == null || card.prefab == null) return;
            Spell prefabSpell = card.prefab.GetComponent<Spell>();
            if (prefabSpell == null) return;
            GameObject go = Instantiate(card.prefab, position, Quaternion.identity);
            go.GetComponent<Spell>().Initialize(card, (Team)team, position, true);
        }
    }
}
