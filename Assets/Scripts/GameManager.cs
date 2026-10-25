using System.Collections;
using Unity.Netcode;
using UnityEngine;

public enum GameState : byte { Waiting, Flying, Bombing, GameOver }
public enum Sfx : byte { FrontGun, GunnerGun, PlayerHit, Repair, WeaponPickup }

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject planePrefab;

    [Header("Audio (drag in from Assets/Audio)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip frontGunClip;
    [SerializeField] private AudioClip gunnerGunClip;
    [SerializeField] private AudioClip playerHitClip;
    [SerializeField] private AudioClip repairClip;
    [SerializeField] private AudioClip weaponPickupClip;
    [SerializeField] private AudioClip gameOverClip;

    [Header("Tuning")]
    [SerializeField] private int startLives = 3;
    [SerializeField] private int maxAmmo = 30;
    [SerializeField] private float roundDistance = 60f;
    [SerializeField] private float flightSpeed = 3f;        // distance units per second
    [SerializeField] private float ammoRegenInterval = 0.5f;
    [SerializeField] private float qteDuration = 4f;
    [SerializeField] private float qteSpeed = 1.5f;
    [SerializeField] private bool missEndsGame = true;      // false = missing costs 1 life instead

    // Green zone on the QTE slider 
    public const float ZoneMin = 0.35f;
    public const float ZoneMax = 0.65f;


    public NetworkVariable<GameState> state = new NetworkVariable<GameState>(GameState.Waiting);
    public NetworkVariable<int> planeLives = new NetworkVariable<int>(3);
    public NetworkVariable<int> score = new NetworkVariable<int>(0);
    public NetworkVariable<int> currentAmmo = new NetworkVariable<int>(30);
    public NetworkVariable<int> distance = new NetworkVariable<int>(60);
    public NetworkVariable<bool> enemyActive = new NetworkVariable<bool>(false);

    [HideInInspector] public double qteStartTime;

    private float distanceRemaining;
    private float ammoTimer;
    private double qteEndTime;

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public override void OnNetworkSpawn()
    {

        state.OnValueChanged += HandleStateChanged;

        if (!IsServer) return;

        planeLives.Value = startLives;
        currentAmmo.Value = maxAmmo;
        distanceRemaining = roundDistance;
        distance.Value = (int)roundDistance;

        GameObject plane = Instantiate(planePrefab, new Vector3(0f, -3.5f, 0f), Quaternion.identity);
        plane.GetComponent<NetworkObject>().Spawn();
    }

    public override void OnNetworkDespawn()
    {
        state.OnValueChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState oldState, GameState newState)
    {
        if (newState == GameState.GameOver) PlayClip(gameOverClip);
    }

    private void Update()
    {
        if (!IsSpawned) return;


        if (state.Value == GameState.Bombing && !NetworkManager.Singleton.IsHost
            && Input.GetKeyDown(KeyCode.Space))
        {
            SubmitQTERpc();
        }

        if (!IsServer) return;

        switch (state.Value)
        {
            case GameState.Waiting:
                if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
                {
                    state.Value = GameState.Flying;
                    enemyActive.Value = true;
                }
                break;

            case GameState.Flying:
                distanceRemaining -= flightSpeed * Time.deltaTime;
                distance.Value = Mathf.CeilToInt(Mathf.Max(0f, distanceRemaining));

                ammoTimer += Time.deltaTime;
                if (ammoTimer >= ammoRegenInterval)
                {
                    ammoTimer = 0f;
                    if (currentAmmo.Value < maxAmmo) currentAmmo.Value++;
                }

                if (distanceRemaining <= 0f) BeginBombing();
                break;

            case GameState.Bombing:
                if (NetworkManager.ServerTime.Time >= qteEndTime) ResolveQTE(false);
                break;
        }
    }


    private void BeginBombing()
    {
        state.Value = GameState.Bombing; 
        enemyActive.Value = false;        

        foreach (var f in FindObjectsByType<FallingObject>(FindObjectsSortMode.None))
            f.ServerDespawn();              

        qteStartTime = NetworkManager.ServerTime.Time;
        qteEndTime = qteStartTime + qteDuration;
        BombingQTERpc(qteStartTime);         
    }

    public float MarkerAt(double secondsSinceStart)
    {
        return Mathf.PingPong((float)secondsSinceStart * qteSpeed, 1f);
    }

    private void ResolveQTE(bool hit)
    {
        if (!hit && missEndsGame) { EndGame(); return; }

        distanceRemaining = roundDistance; 
        distance.Value = (int)roundDistance;
        enemyActive.Value = true;
        state.Value = GameState.Flying;

        if (hit) score.Value += 500;
        else DamagePlane(1);
    }

    private void EndGame()
    {
        state.Value = GameState.GameOver;
        enemyActive.Value = false;
    }


    public void AddScore(int amount)
    {
        if (!IsServer) return;
        score.Value += amount;
    }

    public void DamagePlane(int amount)
    {
        if (!IsServer || state.Value == GameState.GameOver) return;
        planeLives.Value = Mathf.Max(0, planeLives.Value - amount);
        TakeDmgRpc();
        if (planeLives.Value <= 0) EndGame();
    }

    public void RepairPlane()
    {
        if (!IsServer) return;
        planeLives.Value = Mathf.Min(planeLives.Value + 1, startLives);
    }

    public bool TryUseAmmo()
    {
        if (!IsServer || currentAmmo.Value <= 0) return false;
        currentAmmo.Value--;
        return true;
    }

    public void RefillAmmo()
    {
        if (!IsServer) return;
        currentAmmo.Value = maxAmmo;
    }

    public void PlaySfx(Sfx sfx)
    {
        if (!IsServer) return;
        PlaySfxRpc(sfx);
    }


    private void PlayClip(AudioClip clip)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
    }


    [Rpc(SendTo.Everyone)]
    private void PlaySfxRpc(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.FrontGun: PlayClip(frontGunClip); break;
            case Sfx.GunnerGun: PlayClip(gunnerGunClip); break;
            case Sfx.PlayerHit: PlayClip(playerHitClip); break;
            case Sfx.Repair: PlayClip(repairClip); break;
            case Sfx.WeaponPickup: PlayClip(weaponPickupClip); break;
        }
    }

    [Rpc(SendTo.Everyone)]
    private void BombingQTERpc(double startTime)
    {
        qteStartTime = startTime;
    }

    [Rpc(SendTo.Server)]
    private void SubmitQTERpc(RpcParams rpcParams = default)
    {
        if (state.Value != GameState.Bombing) return;

        if (rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId) return;

        float marker = MarkerAt(NetworkManager.ServerTime.Time - qteStartTime);
        ResolveQTE(marker >= ZoneMin && marker <= ZoneMax);
    }

    [Rpc(SendTo.Everyone)]
    private void TakeDmgRpc()
    {
        PlayClip(playerHitClip);
        if (Camera.main != null) StartCoroutine(ShakeCamera());
    }

    private IEnumerator ShakeCamera()
    {
        Transform cam = Camera.main.transform;
        Vector3 origin = cam.position;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            cam.position = origin + (Vector3)(Random.insideUnitCircle * 0.15f);
            yield return null;
        }
        cam.position = origin;
    }
}