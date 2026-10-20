using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardBattle
{
    public enum MatchState { Waiting, Running, Ended, Paused }
    public enum MatchResult { None, Victory, Defeat, Draw }

    /// <summary>
    /// Owns the match lifecycle: countdown, timer, pause, resource regeneration switch, win/lose evaluation.
    /// Towers are discovered automatically through <see cref="Tower.Registry"/>.
    ///
    /// Online: the SERVER (host) runs all of this. Remote clients never decide anything; the Remote* methods below
    /// receive state from NetworkMatchManager and raise the same events, so the HUD and audio code is unchanged.
    ///
    /// <see cref="Result"/> is always from the point of view of Team.Player (the host). The <see cref="MatchEnded"/>
    /// event delivers the result relative to the local player (a remote client sees Victory/Defeat swapped).
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance { get; private set; }
        public static bool IsRunning { get { return Instance != null && Instance.State == MatchState.Running; } }

        [SerializeField] float matchDuration = 180f;
        [SerializeField] float startDelay = 2f;
        [Tooltip("When this many seconds remain, resource regeneration is multiplied.")]
        [SerializeField] float doubleResourceTime = 60f;
        [SerializeField] float doubleResourceMultiplier = 2f;

        readonly List<Tower> playerTowers = new List<Tower>();
        readonly List<Tower> enemyTowers = new List<Tower>();
        bool doubleApplied;
        Coroutine countdown;

        public MatchState State { get; private set; }
        /// <summary>From the point of view of Team.Player.</summary>
        public MatchResult Result { get; private set; }
        public string EndReason { get; private set; }
        public float TimeRemaining { get; private set; }
        public IList<Tower> PlayerTowers { get { return playerTowers; } }
        public IList<Tower> EnemyTowers { get { return enemyTowers; } }
        /// <summary>The local player's towers / the opponent's towers (left to right as seen on THIS screen).</summary>
        public IList<Tower> OwnTowers { get { return NetworkGame.LocalTeam == Team.Player ? playerTowers : enemyTowers; } }
        public IList<Tower> OpponentTowers { get { return NetworkGame.LocalTeam == Team.Player ? enemyTowers : playerTowers; } }

        public event Action MatchStarted;
        public event Action<int> TimerChanged;          // whole seconds remaining
        public event Action DoubleResourceStarted;
        public event Action<MatchResult> MatchEnded;    // relative to the local player
        public event Action<MatchState> StateChanged;

        void Awake()
        {
            Instance = this;
            State = MatchState.Waiting;
            TimeRemaining = matchDuration;
        }

        void Start()
        {
            foreach (Tower t in Tower.Registry)
            {
                (t.Team == Team.Player ? playerTowers : enemyTowers).Add(t);
                t.Died += OnTowerDied;
            }
            // Left tower, main tower, right tower as seen on this screen (the remote client's camera is turned around).
            float sign = NetworkGame.ViewSign;
            Comparison<Tower> byScreenX = (a, b) => (a.transform.position.x * sign).CompareTo(b.transform.position.x * sign);
            playerTowers.Sort(byScreenX);
            enemyTowers.Sort(byScreenX);

            // Offline the match starts by itself. Online the server starts it once both players report ready.
            if (!NetworkGame.IsOnline) BeginCountdown();
            if (NetworkBootstrap.Instance != null) NetworkBootstrap.Instance.SetMatchActive(true);
        }

        void OnDestroy()
        {
            if (Instance == this && NetworkBootstrap.Instance != null) NetworkBootstrap.Instance.SetMatchActive(false);
        }

        // ---------------- server / offline ----------------

        public void BeginCountdown()
        {
            if (State != MatchState.Waiting || countdown != null || !NetworkGame.Authority) return;
            countdown = StartCoroutine(BeginRoutine());
        }

        IEnumerator BeginRoutine()
        {
            yield return new WaitForSeconds(startDelay);
            countdown = null;
            StartMatch();
        }

        public void StartMatch()
        {
            if (State != MatchState.Waiting || !NetworkGame.Authority) return;
            SetState(MatchState.Running);
            if (ResourceManager.Instance != null) ResourceManager.Instance.SetRunning(true);
            if (MatchStarted != null) MatchStarted();
            StartCoroutine(TimerRoutine(matchDuration));
        }

        // Coroutine timer: wakes 10x/second instead of running every frame.
        IEnumerator TimerRoutine(float seconds)
        {
            float endTime = Time.time + seconds;
            int lastSecond = -1;
            var wait = new WaitForSeconds(0.1f);

            while (State == MatchState.Running)
            {
                TimeRemaining = Mathf.Max(0f, endTime - Time.time);
                int sec = Mathf.CeilToInt(TimeRemaining);
                if (sec != lastSecond)
                {
                    lastSecond = sec;
                    if (TimerChanged != null) TimerChanged(sec);
                }
                if (!doubleApplied && TimeRemaining <= doubleResourceTime)
                {
                    doubleApplied = true;
                    if (ResourceManager.Instance != null) ResourceManager.Instance.SetRegenMultiplier(doubleResourceMultiplier);
                    if (DoubleResourceStarted != null) DoubleResourceStarted();
                }
                if (TimeRemaining <= 0f) { EndByTimeout(); yield break; }
                yield return wait;
            }
        }

        /// <summary>Server: freezes the whole match (units, towers, spells, timer, mana) or resumes it.</summary>
        public void SetPaused(bool paused)
        {
            if (!NetworkGame.Authority) return;
            if (paused && State == MatchState.Running)
            {
                SetState(MatchState.Paused);
                if (ResourceManager.Instance != null) ResourceManager.Instance.SetRunning(false);
            }
            else if (!paused && State == MatchState.Paused)
            {
                SetState(MatchState.Running);
                if (ResourceManager.Instance != null) ResourceManager.Instance.SetRunning(true);
                StartCoroutine(TimerRoutine(TimeRemaining)); // continue with the time that was left
            }
        }

        void OnTowerDied(Damageable d)
        {
            if (!NetworkGame.Authority || State != MatchState.Running) return;
            Tower tower = d as Tower;
            if (tower != null && tower.IsMainTower)
                EndMatch(tower.Team == Team.Enemy ? MatchResult.Victory : MatchResult.Defeat, null);
        }

        // Timeout rule: most towers destroyed wins; tie -> most total tower health remaining; else draw.
        void EndByTimeout()
        {
            int crownsPlayer = CrownsFor(Team.Player);
            int crownsEnemy = CrownsFor(Team.Enemy);
            MatchResult result;
            if (crownsPlayer != crownsEnemy) result = crownsPlayer > crownsEnemy ? MatchResult.Victory : MatchResult.Defeat;
            else
            {
                float hp = TotalTowerHealth(Team.Player), ehp = TotalTowerHealth(Team.Enemy);
                if (Mathf.Abs(hp - ehp) < 1f) result = MatchResult.Draw;
                else result = hp > ehp ? MatchResult.Victory : MatchResult.Defeat;
            }
            EndMatch(result, null);
        }

        /// <summary>Server: a player left (or never came back), so the other side wins.</summary>
        public void EndByForfeit(Team leaver, string reason)
        {
            if (State == MatchState.Ended) return;
            EndMatch(leaver == Team.Player ? MatchResult.Defeat : MatchResult.Victory, reason);
        }

        /// <param name="playerPerspective">Result from the point of view of Team.Player.</param>
        public void EndMatch(MatchResult playerPerspective, string reason)
        {
            if (!NetworkGame.Authority || State == MatchState.Ended) return;
            Result = playerPerspective;
            EndReason = reason;
            SetState(MatchState.Ended);
            if (ResourceManager.Instance != null) ResourceManager.Instance.SetRunning(false);
            if (MatchEnded != null) MatchEnded(ForLocalPlayer(playerPerspective));
        }

        // ---------------- remote clients (called by NetworkMatchManager) ----------------

        public void RemoteSetState(MatchState s)
        {
            if (NetworkGame.Authority || State == MatchState.Ended || s == State) return;
            MatchState previous = State;
            SetState(s);
            if (previous == MatchState.Waiting && s == MatchState.Running && MatchStarted != null) MatchStarted();
        }

        public void RemoteTimer(int seconds)
        {
            if (NetworkGame.Authority) return;
            TimeRemaining = seconds;
            if (TimerChanged != null) TimerChanged(seconds);
        }

        public void RemoteDoubleResource()
        {
            if (!NetworkGame.Authority && DoubleResourceStarted != null) DoubleResourceStarted();
        }

        public void RemoteEnd(MatchResult playerPerspective, string reason)
        {
            if (NetworkGame.Authority || State == MatchState.Ended) return;
            Result = playerPerspective;
            EndReason = reason;
            SetState(MatchState.Ended);
            if (MatchEnded != null) MatchEnded(ForLocalPlayer(playerPerspective));
        }

        // ---------------- helpers ----------------

        void SetState(MatchState s)
        {
            if (State == s) return;
            State = s;
            if (StateChanged != null) StateChanged(s);
        }

        /// <summary>Victory/Defeat from the local player's seat (the remote client sits on Team.Enemy).</summary>
        public static MatchResult ForLocalPlayer(MatchResult playerPerspective)
        {
            if (NetworkGame.LocalTeam == Team.Player || playerPerspective == MatchResult.Draw || playerPerspective == MatchResult.None) return playerPerspective;
            return playerPerspective == MatchResult.Victory ? MatchResult.Defeat : MatchResult.Victory;
        }

        /// <summary>Number of enemy towers <paramref name="team"/> has destroyed.</summary>
        public int CrownsFor(Team team)
        {
            List<Tower> victims = team == Team.Player ? enemyTowers : playerTowers;
            int n = 0;
            for (int i = 0; i < victims.Count; i++) if (victims[i] == null || victims[i].IsDead) n++;
            return n;
        }

        public float TotalTowerHealth(Team team)
        {
            List<Tower> list = team == Team.Player ? playerTowers : enemyTowers;
            float sum = 0f;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && !list[i].IsDead) sum += list[i].Health;
            return sum;
        }

        /// <summary>Offline: reload the scene. Online: only the host can ask for a rematch (the server reloads the battle).</summary>
        public void Restart()
        {
            if (NetworkGame.IsOnline)
            {
                if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.RequestRematch();
                return;
            }
            if (GameManager.Instance != null) GameManager.Instance.RestartMatch();
            else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
