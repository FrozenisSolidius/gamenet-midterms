using Unity.Netcode;
using UnityEngine;

public class PlaneController : NetworkBehaviour
{
    [SerializeField] private Transform gunPod;
    [SerializeField] private Transform gunMuzzle;
    [SerializeField] private Transform frontMuzzle;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Gunner gun sounds")]
    [SerializeField] private AudioSource gunLoopSource;   
    [SerializeField] private AudioSource gunTailSource;   

    [Header("Tuning")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float xLimit = 2.5f;
    [SerializeField] private float pilotFireCooldown = 0.6f;   
    [SerializeField] private float gunnerFireCooldown = 0.15f; 
    [SerializeField] private float pilotBulletSpeed = 8f;
    [SerializeField] private float gunnerBulletSpeed = 12f;
    [SerializeField] private float frontGunsDuration = 15f;    // seconds the pickup lasts

    public NetworkVariable<float> gunAngle = new NetworkVariable<float>(0f);
    public NetworkVariable<bool> frontGunsActive = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> gunnerFireActive = new NetworkVariable<bool>(false);

    // Server-only
    private float moveInput;
    private bool pilotFiring, gunnerFiring;
    private float nextPilotShot, nextGunnerShot;
    private float frontGunsEndTime;

    // Client-only
    private float lastSentMove = 99f;
    private bool lastSentPilotFire, lastSentGunnerFire;
    private float lastSentAngle = 1000f;
    private Camera cam;

    public override void OnNetworkSpawn()
    {
        cam = Camera.main;
        gunnerFireActive.OnValueChanged += HandleGunnerFireChanged;

        if (gunnerFireActive.Value) StartGunLoop();
    }

    public override void OnNetworkDespawn()
    {
        gunnerFireActive.OnValueChanged -= HandleGunnerFireChanged;
    }

    private void HandleGunnerFireChanged(bool wasFiring, bool isFiring)
    {
        if (isFiring) StartGunLoop();
        else StopGunLoop();
    }

    private void StartGunLoop()
    {
        if (gunTailSource != null) gunTailSource.Stop();
        if (gunLoopSource != null && !gunLoopSource.isPlaying) gunLoopSource.Play();
    }

    private void StopGunLoop()
    {
        if (gunLoopSource != null && gunLoopSource.isPlaying)
        {
            gunLoopSource.Stop();
            if (gunTailSource != null) gunTailSource.Play();  // the "lastshot"
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        bool localIsPilot = NetworkManager.Singleton.IsHost;

        HandleLocalInput(localIsPilot);

        if (localIsPilot || !NetworkManager.Singleton.IsClient)
            gunPod.localRotation = Quaternion.Euler(0f, 0f, gunAngle.Value);

        if (IsServer) ServerTick();
    }


    public void ArmFrontGuns()
    {
        if (!IsServer) return;
        frontGunsActive.Value = true;
        frontGunsEndTime = Time.time + frontGunsDuration;
    }

    private void HandleLocalInput(bool localIsPilot)
    {
        if (localIsPilot)
        {
            float h = Input.GetAxisRaw("Horizontal");
            if (h != lastSentMove) { lastSentMove = h; MoveInputRpc(h); }

            bool fire = Input.GetKey(KeyCode.Space);
            if (fire != lastSentPilotFire) { lastSentPilotFire = fire; PilotFireRpc(fire); }
        }
        else
        {
            Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 dir = (Vector2)mouse - (Vector2)gunPod.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;

            gunPod.localRotation = Quaternion.Euler(0f, 0f, angle); // instant local feel

            if (Mathf.Abs(Mathf.DeltaAngle(angle, lastSentAngle)) > 1f)
            {
                lastSentAngle = angle;
                AimRpc(angle);
            }

            bool fire = Input.GetMouseButton(0);
            if (fire != lastSentGunnerFire) { lastSentGunnerFire = fire; GunnerShootRpc(fire); }
        }
    }

    private static bool FromPilot(RpcParams p) => p.Receive.SenderClientId == NetworkManager.ServerClientId;

    [Rpc(SendTo.Server)]
    private void MoveInputRpc(float h, RpcParams rpcParams = default)
    {
        if (!FromPilot(rpcParams)) return;
        moveInput = Mathf.Clamp(h, -1f, 1f);
    }

    [Rpc(SendTo.Server)]
    private void PilotFireRpc(bool firing, RpcParams rpcParams = default)
    {
        if (!FromPilot(rpcParams)) return;
        pilotFiring = firing;
    }

    [Rpc(SendTo.Server)]
    private void AimRpc(float angle, RpcParams rpcParams = default)
    {
        if (FromPilot(rpcParams)) return;
        gunAngle.Value = angle;
    }

    [Rpc(SendTo.Server)]
    private void GunnerShootRpc(bool firing, RpcParams rpcParams = default)
    {
        if (FromPilot(rpcParams)) return;
        gunnerFiring = firing;
    }

    private void ServerTick()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        bool flying = gm.state.Value == GameState.Flying;


        gunnerFireActive.Value = flying && gunnerFiring && gm.currentAmmo.Value > 0;

        if (!flying) return;

        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x + moveInput * moveSpeed * Time.deltaTime, -xLimit, xLimit);
        transform.position = p;

        if (frontGunsActive.Value && Time.time >= frontGunsEndTime)
            frontGunsActive.Value = false;


        if (pilotFiring && frontGunsActive.Value && Time.time >= nextPilotShot)
        {
            nextPilotShot = Time.time + pilotFireCooldown;
            SpawnBullet(frontMuzzle.position, transform.up, pilotBulletSpeed, true);
            gm.PlaySfx(Sfx.FrontGun);
        }

        if (gunnerFiring && Time.time >= nextGunnerShot)
        {
            nextGunnerShot = Time.time + gunnerFireCooldown;
            if (gm.TryUseAmmo())
                SpawnBullet(gunMuzzle.position, gunPod.up, gunnerBulletSpeed, false); 
        }
    }

    private void SpawnBullet(Vector3 pos, Vector3 dir, float speed, bool heavy)
    {
        GameObject go = Instantiate(bulletPrefab, pos, Quaternion.identity);
        go.transform.up = dir;
        Bullet b = go.GetComponent<Bullet>();
        b.speed = speed;
        b.heavy = heavy;
        go.GetComponent<NetworkObject>().Spawn(true);
    }
}