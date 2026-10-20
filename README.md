# Online 1v1 multiplayer (Netcode for GameObjects, listen-server)

Status: all scripts compile against stubs of the Unity and Netcode APIs, but this has NOT been run in a real Unity editor with a real
Netcode package. Expect to fix small API differences on first import and play-test it. Send me the exact console error if something fails.

## 1. What I inspected and what I decided
The existing game already had one simulation (Unit / Tower / CardManager / ResourceManager / MatchManager) and all decisions go through
`CardManager.TryDeploy`, so the smallest safe change is **server-authoritative replication of the existing simulation**, not a rewrite:

| Choice | Why |
|---|---|
| Netcode for GameObjects + Unity Transport (UDP) | Official, works in Editor + builds, supports host mode (server + local player in ONE process, so no second computer). |
| Listen-server: host = server + Player 1 (Team.Player), friend = Player 2 (Team.Enemy) | What you asked for. All networking sits in `Scripts/Networking`, so a dedicated server can replace the host later. |
| Existing gameplay classes keep their rules; a small `NetworkEntity` component is added next to them | No duplicated game. On the server `Unit/Tower/Building` run exactly as before; on the client they are inert and only display. |
| Offline mode still works | `NetworkGame.Authority` is true when no network is running, so "Play vs AI" is unchanged. |

### Who owns what
| System | Server (host) | Client |
|---|---|---|
| Match state, timer, result | decides; publishes via `NetworkMatchManager` (state + seconds NetworkVariables, end result RPC) | displays |
| Seats / identity | server assigns slot 0 (host) / 1 (guest) at connection approval and spawns each `NetworkPlayer` itself | cannot choose |
| Deck, hand, card queue | dealt and cycled on the server (`PlayerSlot`/`DeckModel`) | gets ONLY its own hand (owner-only NetworkList); opponent hand is never sent |
| Mana | server pool (`ResourceManager`), regeneration on the server | gets its own mana in tenths (owner-only, ~3-4 messages/s) |
| Deploy | validates request, hand slot and card id, location, cost, spends mana, spawns | sends "deploy hand slot N (card id) at position" and nothing else |
| Units / buildings | simulation (movement, targeting, combat, abilities, spells); spawned as NetworkObjects | puppets: position via NetworkTransform (interpolated), health/facing/stun via NetworkVariables |
| Towers | simulation + destruction | replicated health, destruction effects |
| Spells | applied only on the server | visual-only copy via RPC |
| Cosmetics (attack animation, projectile visuals, explosions, lightning) | played locally, then broadcast via RPC | played from RPC; losing one is harmless |

Rejected or ignored on the server: bad slot or card id, stale/duplicate request ids, NaN positions, wrong side of the arena, blocked
location, not enough mana, requests while the match is not running, more than ~12 requests/second.

## 2. Packages
Window > Package Manager > Unity Registry > **Netcode for GameObjects** > Install. (Unity Transport and Collections install as dependencies.)
- Unity 6: Netcode 2.x. Unity 2021.3 / 2022: Netcode 1.x. The scripts use `[ServerRpc]/[ClientRpc]`, `NetworkVariable`, `NetworkList`,
  `UnityTransport.SetConnectionData`, connection approval: all valid in 1.x and 2.x.
- Optional for testing in the editor: Unity 6 "Multiplayer Play Mode" (Window > Multiplayer > Multiplayer Play Mode).

## 3. Setup (once)
1. Install Netcode (above). Copy the `CardBattle` folder into `Assets/` and wait for the compile.
2. Player Settings: Active Input Handling = Both. Set **Version** (Player > Other Settings) to the same value for host and friend.
3. **Tools > Card Battle > Build Prototype Scene**. It now also: adds NetworkObject (+NetworkTransform for units) and NetworkEntity to every
   unit/building/tower prefab, creates the `NetworkPlayer` prefab, adds the `NetworkMatch` object to the Battle scene, builds
   `Assets/CardBattle/Scenes/Menu.unity` and sets Build Settings to [Menu, Battle]. (Re-running overwrites generated assets.)
4. **Tools > Card Battle > Fix Build Settings** (shaders, portrait, run in background).
5. Open `Menu.unity` and press Play.

### NetworkManager / Unity Transport: nothing to configure by hand
`NetworkBootstrap` (Menu scene, DontDestroyOnLoad) creates the NetworkManager + UnityTransport at runtime, or re-uses one you place in the scene.
It sets: Connection Approval ON, Player Prefab empty (the server spawns players itself), Run In Background ON, Tick Rate default (30),
transport MaxConnectAttempts 5, ConnectTimeout 2000 ms, DisconnectTimeout 10000 ms, host listens on 0.0.0.0, client connects to the IP you type.
If you prefer a manual NetworkManager: Network Transport = Unity Transport, Enable Scene Management ON, Connection Approval ON, Player Prefab EMPTY.

### Registering network prefabs
Automatic. `NetworkBootstrap.RegisterPrefabs()` runs before every host/client start and registers the `NetworkPlayer` prefab plus the prefab of every
card in `Resources/Cards` that has a NetworkObject. Both players register the same list. For a NEW card prefab: add `NetworkObject`, `NetworkEntity`
(+ `NetworkTransform` if it moves) to the prefab, and keep its CardData in a `Resources/Cards` folder.

### Menu (already wired)
`MenuUI` (NetworkConnectionUI) builds the screens from code, no buttons to connect by hand:
- **Main:** Play vs AI / Host online game / Join online game
- **Host:** port, START SERVER AND PLAY, status, listening port, players 1/2, local (LAN/VPN) addresses, SHOW MY PUBLIC IP (optional request to api.ipify.org), COPY ADDRESS, START MATCH (enabled with 2 players), STOP SERVER
- **Join:** IP, port, CONNECT, status, error text, DISCONNECT

Connection states shown: Disconnected, Connecting, Connected, Waiting for opponent, Lobby ready, Match in progress, Connection lost.

## 4. Test on ONE laptop
Option A (Editor host + built client): Tools > Card Battle > Build Windows (.exe). Run the .exe and choose JOIN with `127.0.0.1`, port 7777.
In the Editor open Menu.unity, Play, HOST > START SERVER AND PLAY, then START MATCH when the exe shows connected.
Option B: Unity 6 Multiplayer Play Mode: enable a Virtual Player, Play both, host in the main editor, join `127.0.0.1` in the other.
Option C: two builds on the same PC (different windows) also work.
You should see: two different seats (host at the bottom as blue, guest sees their own side at the bottom as blue), separate hands and mana, units
appear on both screens, towers lose health on both.

## 5. Play over the internet
You need ONE of these. A private address like 192.168.x.x does not work over the internet.

### Method A: port forwarding (free, may not work on every ISP)
1. Host listens on **UDP 7777** (change it in the Host screen if you like; the friend uses the same number).
2. Windows Firewall (host PC), administrator Command Prompt:
   `netsh advfirewall firewall add rule name="Card Battle" dir=in action=allow protocol=UDP localport=7777`
   (or accept the Windows prompt the first time and tick Private + Public). macOS: allow incoming connections when asked.
3. Router: log in (usually 192.168.0.1 or 192.168.1.1) > Port Forwarding / Virtual Server > add: protocol **UDP**, external port 7777,
   internal port 7777, internal IP = the host PC's LAN IP (shown in the Host screen). Give the PC a fixed ("reserved") LAN IP.
4. Press SHOW MY PUBLIC IP on the Host screen (or search "what is my IP"), COPY ADDRESS, send it to your friend.
5. Friend: Join > that public IP + port > CONNECT.
Notes: the public IP can change when the router restarts (check again before each session). It will NOT work with CGNAT (your router's WAN IP
differs from your public IP, common on mobile/fiber/some ISPs), double NAT (modem + router), or ISPs/routers that block inbound ports.
If your router's WAN IP starts with 100.64-100.127 or differs from the public IP, use Method B.

### Method B: virtual LAN (recommended when A fails)
- **Tailscale** (free personal plan, needs an account, installs on both PCs; a reputable service): host installs Tailscale, invites/shares the machine with
  the friend, friend installs it and signs in; the host's Tailscale address is `100.x.y.z`. The friend enters THAT address in Join. No router changes.
- Alternatives: ZeroTier (free tier, account + network ID) or Radmin VPN (free, Windows only).
- Unity Relay (Unity Gaming Services) is NOT implemented here: it needs a UGS account/project link, the Relay service, and extra code; it has a free tier
  but is a cloud service. Say the word and I will add it.
Security: the server accepts only 2 players, validates everything, exposes one UDP port, and has no public matchmaking. Transport is not encrypted;
only play with people you trust and close the port when finished.

## 6. Match lifecycle
Host starts server > friend joins (seat 2) > both see "Lobby ready" > host presses START MATCH > server deals a random deck per player and loads Battle
for both > both HUDs report ready > 2 s countdown > match > server ends it > both see the same result (Victory/Defeat swapped for the guest) >
host: REMATCH or LEAVE; guest: LEAVE (waits for host).
Disconnect during a match (NetworkGameManager, `Disconnect Policy`): **PauseThenEnd** (default): match pauses, banner shows a countdown
(`Reconnect Grace Seconds` = 30); the same game session can reconnect and continue with its hand; otherwise the other player wins.
**EndMatch**: the remaining player wins immediately. If the host stops the server, guests see "connection lost" and return to the menu:
a listen-server match cannot continue without the host.
Hand size online = 4 of an 8-card deck (cycling); change `Hand Size` on NetworkGameManager (8 = whole deck visible).

## 7. Phases: files changed and how to test
| Phase | Files | Test |
|---|---|---|
| 1 Inspect | none | n/a |
| 2 Packages, NetworkManager | Networking/NetworkBootstrap.cs, NetworkGameState.cs | Menu > Host: status "Server running" |
| 3 Host / client connect | NetworkBootstrap.cs, NetworkGameManager.cs, UI/NetworkConnectionUI.cs | friend (or 2nd instance) joins; wrong IP gives an error in ~12 s |
| 4 Lobby / 2 seats | NetworkGameManager.cs, NetworkPlayer.cs | third connection is rejected "match is full" |
| 5 Towers + match lifecycle | Core/MatchManager.cs, NetworkMatchManager.cs, NetworkEntity.cs | both screens show equal timer and tower HP |
| 6 Deploy, mana, units, combat | NetworkPlayer.cs, Cards/CardManager.cs, DeckManager.cs, ResourceManager.cs, Units/Unit.cs, Projectile.cs, PlayerController.cs, BattleUI.cs | deploy from either side; units match on both screens |
| 7 Buildings, spells, cycling | Buildings/*, Spells/Spell.cs, Abilities (NetFx hooks) | towers/buildings shoot, spells show on both |
| 8 Disconnect, results | NetworkGameManager.cs, MatchManager.cs, ConnectionStatusUI.cs, BattleUI.cs | kill the guest process mid-match |
| 9 Two-client test | Editor/CardBattleSetup*.cs | section 4 |
| 10 Docs | this file | n/a |

New: `Scripts/Networking/{NetworkBootstrap, NetworkGameManager, NetworkPlayer, NetworkEntity, NetworkMatchManager, NetworkGameState, CardRegistry}.cs`,
`UI/{NetworkConnectionUI, ConnectionStatusUI}.cs`, `Editor/CardBattleSetup.Network.cs`.
Modified: Team, Damageable, Unit, RangedUnit, Projectile, Building, DefensiveBuilding, SpawnerBuilding, Spell, DeathBurst/ChainAttack abilities,
CardManager, DeckManager, ResourceManager, MatchManager (rewritten; `EndMatch` now takes a reason), PlayerController, BattleCameraController, BattleUI,
AIPlayer, editor builder + build helper. Removed/renamed: none.

## 8. Known limitations (honest list)
- Not replicated (cosmetic only): the Vanguard shield bubble, aura rings, charge dust, lifesteal sparks, slow tint, deploy dust ring for the opponent's cards.
  Gameplay effects of all of these ARE applied on the server and reflected in health/position.
- Reconnection works within one app session (the secret token is per session). After restarting the game the old seat is not recognised.
- No encryption, no matchmaking, no relay, no ping display.
- Not yet tested against a real Netcode install: see the status note at the top.

## 9. Troubleshooting
| Problem | Fix |
|---|---|
| "Timed out" / "Could not reach the server" | Wrong IP/port; host not running; firewall; port forwarding missing or CGNAT (use Tailscale). On the same PC use 127.0.0.1. |
| Works on the same Wi-Fi, not over the internet | Port forward UDP (not TCP), correct internal IP, public IP not the router's WAN IP, CGNAT/double NAT. |
| "Could not start the server on port N" | Another program uses the port: pick another (e.g. 7778) on both sides. |
| "Version mismatch" / "Card data mismatch" | Both must run the same build: same Player Settings > Version, same Resources/Cards content. Rebuild and send the new build. |
| "The match is full" | Two players already seated; or a match is already running. |
| Client stuck on "Connected, joining lobby" | Menu and Battle must both be in Build Settings; run Fix Build Settings. |
| Prefab error about GlobalObjectIdHash / not registered | Re-run Build Prototype Scene, or select the prefab, Reimport, Ctrl+S. New prefabs need NetworkObject + NetworkEntity. |
| "Unit prefab has no NetworkObject" in the console | Same as above (old prefabs). Re-run the builder. |
| Game freezes when the window loses focus | Run In Background must be on (Fix Build Settings does it). |
| Pink screen in build | Tools > Card Battle > Fix Build Settings, Quality settings step in README. |
| Units appear but do not move on the guest | Units need NetworkTransform; re-run the builder. |
| `InvalidOperationException ... Input` | Active Input Handling = Both. |
