using Unity.Netcode;
using UnityEngine;

namespace CardBattle
{
    /// <summary>The connection states shown in the UI.</summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,          // connected to a server, waiting for the lobby to sync
        WaitingForOpponent, // in the lobby, only one player present
        LobbyReady,         // both player slots filled, waiting for the host to start
        MatchInProgress,
        ConnectionLost
    }

    /// <summary>
    /// Single place that answers "who is allowed to run gameplay?" and "which side am I?".
    ///  - Offline (no NetworkManager running): this process is authoritative, I am Team.Player.
    ///  - Server / host: authoritative. The host is always slot 0 = Team.Player.
    ///  - Remote client: NOT authoritative; renders replicated state. Slot 1 = Team.Enemy.
    /// Everything in the existing game that "makes decisions" checks <see cref="Authority"/>.
    /// </summary>
    public static class NetworkGame
    {
        public const int ProtocolVersion = 1;
        public const int MaxPlayers = 2;

        static Team localTeam = Team.Player;

        /// <summary>The side this machine controls. The camera, HUD colours and deployment rules are relative to it.</summary>
        public static Team LocalTeam { get { return localTeam; } }
        public static Team OpponentTeam { get { return localTeam.Opposite(); } }
        /// <summary>-1 when the camera is turned around (remote client), +1 otherwise.</summary>
        public static float ViewSign { get { return localTeam == Team.Enemy ? -1f : 1f; } }

        public static bool IsOnline
        {
            get { NetworkManager nm = NetworkManager.Singleton; return nm != null && nm.IsListening; }
        }

        public static bool IsServer
        {
            get { NetworkManager nm = NetworkManager.Singleton; return nm != null && nm.IsListening && nm.IsServer; }
        }

        /// <summary>True when this process should simulate the game (offline, or the server/host).</summary>
        public static bool Authority { get { return !IsOnline || NetworkManager.Singleton.IsServer; } }

        public static void SetLocalTeam(Team team)
        {
            localTeam = team;
            TeamUtil.ViewTeam = team;
        }

        public static void ResetToOffline() { SetLocalTeam(Team.Player); }

        /// <summary>Server only: replicates a freshly created unit/building to every client. No-op offline.</summary>
        public static void SpawnEntity(GameObject go, CardData card, Team team)
        {
            if (!IsServer) return;
            NetworkObject no = go.GetComponent<NetworkObject>();
            NetworkEntity ne = go.GetComponent<NetworkEntity>();
            if (no == null || ne == null)
            {
                Debug.LogError("Online play: prefab '" + go.name + "' has no NetworkObject / NetworkEntity. Re-run Tools > Card Battle > Build Prototype Scene.");
                UnityEngine.Object.Destroy(go);
                return;
            }
            ne.ServerSetup(CardRegistry.IdOf(card), team);
            no.Spawn(true);
        }

        /// <summary>
        /// Removes an entity. Offline: Destroy. Server: Despawn (clients delete their copy when the despawn arrives).
        /// Remote client: does nothing, it must never delete a replicated object itself.
        /// </summary>
        public static void DestroyEntity(GameObject go)
        {
            if (go == null) return;
            NetworkObject no = go.GetComponent<NetworkObject>();
            if (IsOnline && no != null && no.IsSpawned)
            {
                if (NetworkManager.Singleton.IsServer) no.Despawn(true);
                return;
            }
            UnityEngine.Object.Destroy(go);
        }
    }

    /// <summary>
    /// Cosmetic effects. Each call plays the effect locally (offline / host) and, on the server, tells remote clients
    /// to play it too. Nothing here changes gameplay state, so losing one of these messages is harmless.
    /// </summary>
    public static class NetFx
    {
        public static void Explosion(Vector3 pos, float radius, Color color, float shake)
        {
            Vfx.Explosion(pos, radius, color);
            if (shake > 0f && BattleCameraController.Instance != null) BattleCameraController.Instance.Shake(shake, 0.25f);
            if (NetworkGame.IsServer && NetworkMatchManager.Instance != null) NetworkMatchManager.Instance.ExplosionClientRpc(pos, radius, color, shake);
        }

        public static void Bolt(Vector3 from, Vector3 to, Color color)
        {
            Vfx.Bolt(from, to, color);
            if (NetworkGame.IsServer && NetworkMatchManager.Instance != null) NetworkMatchManager.Instance.BoltClientRpc(from, to, color);
        }

        /// <summary>Server tells clients to show a spell's visuals (clients never apply the spell's effect).</summary>
        public static void SpellCast(CardData card, Team team, Vector3 pos)
        {
            if (NetworkGame.IsServer && NetworkMatchManager.Instance != null)
                NetworkMatchManager.Instance.SpellClientRpc(CardRegistry.IdOf(card), (byte)team, pos);
        }
    }
}
