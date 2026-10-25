using Unity.Netcode;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    [HideInInspector] public float speed = 8f;
    [HideInInspector] public bool heavy;          // 50mm rounds
    [SerializeField] private int damage = 1;
    [SerializeField] private int heavyDamage = 5; // enough to pop a balloon in one hit
    [SerializeField] private float lifeTime = 3f;

    public override void OnNetworkSpawn()
    {
        if (IsServer) Invoke(nameof(Kill), lifeTime);
    }

    private void Update()
    {
        if (!IsServer) return;
        transform.position += transform.up * speed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        if (!other.TryGetComponent(out FallingObject target)) return;

        if (target.destroyedByBullets || heavy)
        {
            target.TakeHit(heavy ? heavyDamage : damage);
            Kill();
        }
    }

    private void Kill()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }
}