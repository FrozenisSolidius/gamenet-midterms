using Unity.Netcode;
using UnityEngine;

public enum PickupType { Repair, Weapon }

public class Pickup : NetworkBehaviour
{
    [SerializeField] private PickupType type;
    [SerializeField] private float fallSpeed = 1.5f;

    private void Update()
    {
        if (!IsServer) return;
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;
        if (transform.position.y < -7f) Remove();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Server decides who picked it up first, which prevents double pickups
        if (!IsServer) return;
        if (!other.TryGetComponent<PlaneController>(out PlaneController plane)) return;

        GameManager gm = GameManager.Instance;

        if (type == PickupType.Repair)
        {
            gm.RepairPlane();
            gm.PlaySfx(Sfx.Repair);
        }
        else
        {
            plane.ArmFrontGuns();
            gm.PlaySfx(Sfx.WeaponPickup);
        }

        Remove();
    }

    private void Remove()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }
}