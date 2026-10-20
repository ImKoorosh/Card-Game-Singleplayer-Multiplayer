using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// One per connected player (spawned by the server, owned by that player's connection).
    ///
    /// Replicated state:
    ///  - slot (everyone): 0 = host / Team.Player, 1 = guest / Team.Enemy. Assigned by the SERVER, never by the client.
    ///  - hand + mana (owner only): the opponent can never read your cards or your mana.
    /// The deck, the card queue and the real mana pool exist only on the server (see PlayerSlot).
    ///
    /// Clients send exactly one kind of request: "deploy hand slot N at this position".
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        public static readonly List<NetworkPlayer> All = new List<NetworkPlayer>();
        public static NetworkPlayer Local { get; private set; }

        readonly NetworkVariable<byte> slotValue = new NetworkVariable<byte>(0);
        readonly NetworkVariable<int> manaTenths = new NetworkVariable<int>(0, NetworkVariableReadPermission.Owner);
        readonly NetworkList<int> hand = new NetworkList<int>(null, NetworkVariableReadPermission.Owner);

        PlayerSlot slot;            // server only
        int requestCounter;         // client side request ids

        public int Slot { get { return slotValue.Value; } }
        public Team Team { get { return (Team)slotValue.Value; } }
        public bool IsHostPlayer { get { return slotValue.Value == 0; } }

        // ---------------- server API ----------------

        public void ServerInit(PlayerSlot s)
        {
            slot = s;
            slotValue.Value = (byte)s.index;
        }

        /// <summary>New match: fresh hand + mana. Called by the session manager before the battle scene loads.</summary>
        public void ServerBeginMatchState()
        {
            if (slot == null) return;
            slot.lastRequestId = 0;
            slot.lastRequestTime = -1f;
            manaTenths.Value = 50;
            SyncHandFromModel();
        }

        /// <summary>Reconnect during a match: keep the seat's deck/queue, just re-publish the hand.</summary>
        public void ServerBeginMatchStateKeepModel()
        {
            SyncHandFromModel();
        }

        /// <summary>The battle scene is loaded on the server: start mirroring this player's mana pool.</summary>
        public void ServerAttachToBattle()
        {
            if (!IsServer || ResourceManager.Instance == null) return;
            ResourceManager.Instance.Changed -= OnServerManaChanged;
            ResourceManager.Instance.Changed += OnServerManaChanged;
            manaTenths.Value = Mathf.FloorToInt(ResourceManager.Instance.Get(Team) * 10f + 0.001f);
        }

        void OnServerManaChanged(Team team, float value)
        {
            if (team != Team) return;
            int tenths = Mathf.FloorToInt(value * 10f + 0.001f); // ~3-4 updates per second, never per frame
            if (tenths != manaTenths.Value) manaTenths.Value = tenths;
        }

        void SyncHandFromModel()
        {
            hand.Clear();
            if (slot == null || slot.model == null) return;
            for (int i = 0; i < slot.model.Hand.Count; i++) hand.Add(CardRegistry.IdOf(slot.model.Hand[i]));
        }

        // ---------------- lifecycle ----------------

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsOwner)
            {
                Local = this;
                NetworkGame.SetLocalTeam(Team);
            }
            hand.OnListChanged += OnHandChanged;
            manaTenths.OnValueChanged += OnManaChanged;
            NetworkGameManager.NotifyPlayersChanged();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
            hand.OnListChanged -= OnHandChanged;
            manaTenths.OnValueChanged -= OnManaChanged;
            if (IsServer && ResourceManager.Instance != null) ResourceManager.Instance.Changed -= OnServerManaChanged;
            NetworkGameManager.NotifyPlayersChanged();
        }

        // ---------------- local UI sync (owner only) ----------------

        void OnHandChanged(NetworkListEvent<int> e) { PushStateToUI(); }
        void OnManaChanged(int previous, int current) { PushStateToUI(); }

        /// <summary>Copies the replicated hand and mana into the existing DeckManager / ResourceManager (UI side).</summary>
        public void PushStateToUI()
        {
            if (!IsOwner) return;
            DeckManager deck = DeckManager.Instance;
            if (deck != null)
            {
                var ids = new List<int>(hand.Count);
                for (int i = 0; i < hand.Count; i++) ids.Add(hand[i]);
                deck.SetNetworkHand(ids);
            }
            // The server already owns the real pool; only remote clients mirror it.
            if (!IsServer && ResourceManager.Instance != null)
                ResourceManager.Instance.ApplyRemote(NetworkGame.LocalTeam, manaTenths.Value / 10f);
        }

        // ---------------- client -> server requests ----------------

        /// <summary>The battle scene is loaded and the HUD is ready. Lets the server start (or resume) the match.</summary>
        public void NotifyBattleReady()
        {
            if (IsOwner) BattleReadyServerRpc();
        }

        [ServerRpc]
        void BattleReadyServerRpc()
        {
            if (slot == null) return;
            slot.battleReady = true;
            ServerAttachToBattle();
            NetworkGameManager.OnPlayerBattleReady();
        }

        /// <summary>Asks the server to deploy the card in hand slot <paramref name="handSlot"/> at <paramref name="position"/>.</summary>
        public void RequestDeploy(int handSlot, int cardId, Vector3 position)
        {
            if (!IsOwner) return;
            requestCounter++;
            RequestDeployServerRpc(requestCounter, handSlot, cardId, position);
        }

        // Only the owning client may call this (RequireOwnership defaults to true). Everything else is validated again below.
        [ServerRpc]
        void RequestDeployServerRpc(int requestId, int handSlot, int cardId, Vector3 position)
        {
            string message = null;
            DeployResult result = ServerValidateAndDeploy(requestId, handSlot, cardId, position, out message);
            if (result == DeployResult.MatchNotRunning && message == null) return; // silently ignored (duplicate / stale)
            ClientRpcParams toOwner = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };
            DeployResultClientRpc(requestId, handSlot, (byte)result, message ?? "", toOwner);
        }

        DeployResult ServerValidateAndDeploy(int requestId, int handSlot, int cardId, Vector3 position, out string message)
        {
            message = null;
            if (slot == null || slot.model == null) { message = "No active match"; return DeployResult.MatchNotRunning; }

            // Duplicate or out-of-order request (ids only ever increase): ignore.
            if (requestId <= slot.lastRequestId) return DeployResult.MatchNotRunning;
            slot.lastRequestId = requestId;

            // Basic flood protection.
            float now = Time.unscaledTime;
            if (now - slot.lastRequestTime < 0.08f) { message = "Slow down"; return DeployResult.InvalidCard; }
            slot.lastRequestTime = now;

            if (float.IsNaN(position.x) || float.IsNaN(position.z) || float.IsInfinity(position.x) || float.IsInfinity(position.z))
            { message = "Invalid position"; return DeployResult.InvalidLocation; }

            if (!MatchManager.IsRunning) { message = "The match is not running"; return DeployResult.MatchNotRunning; }

            IList<CardData> serverHand = slot.model.Hand;
            if (handSlot < 0 || handSlot >= serverHand.Count) { message = "Invalid card slot"; return DeployResult.InvalidCard; }
            CardData card = serverHand[handSlot];
            if (CardRegistry.IdOf(card) != cardId) { message = "Your hand changed, try again"; return DeployResult.InvalidCard; }

            // Same code path the offline game uses: location rules, cost check and spend, spawn.
            DeployResult result = CardManager.Instance.TryDeploy(Team, card, position, out message);
            if (result == DeployResult.Success)
            {
                slot.model.Cycle(handSlot);
                SyncHandFromModel();
            }
            return result;
        }

        [ClientRpc]
        void DeployResultClientRpc(int requestId, int handSlot, byte result, string message, ClientRpcParams rpcParams = default(ClientRpcParams))
        {
            if (!IsOwner) return;
            if (PlayerController.Instance != null) PlayerController.Instance.OnDeployResult(handSlot, (DeployResult)result, message);
        }
    }
}
