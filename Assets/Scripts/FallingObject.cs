using Unity.Netcode;
using UnityEngine;


public class FallingObject : NetworkBehaviour
{
    public bool destroyedByBullets = true;
    [SerializeField] private int hp = 2;
    [SerializeField] private int damageToPlane = 1;
    [SerializeField] private int scoreValue = 100;
    [SerializeField] private float speed = 2f;

    private void Update()
    {
        if (!IsServer) return;
        transform.position += Vector3.down * speed * Time.deltaTime;
        if (transform.position.y < -7f) ServerDespawn();
    }

    public void TakeHit(int dmg)
    {
        if (!IsServer) return;
        hp -= dmg;
        if (hp <= 0)
        {
            GameManager.Instance.AddScore(scoreValue);
            ServerDespawn();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        if (other.TryGetComponent<PlaneController>(out _))
        {
            GameManager.Instance.DamagePlane(damageToPlane);
            ServerDespawn();
        }
    }

    public void ServerDespawn()
    {
        if (IsServer && NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }
}