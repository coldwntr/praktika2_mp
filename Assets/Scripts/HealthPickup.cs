using Unity.Netcode;
using UnityEngine;

public class HealthPickup : NetworkBehaviour
{
    [SerializeField] private int _healAmount = 35;

    private PickupManager _pickupManager;
    private int _spawnPointIndex = -1;
    private bool _collected;

    public void Initialize(PickupManager pickupManager, int spawnPointIndex)
    {
        _pickupManager = pickupManager;
        _spawnPointIndex = spawnPointIndex;
        _collected = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || _collected)
        {
            return;
        }

        PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
        if (player == null || !player.IsAlive.Value || player.HP.Value >= player.MaxHealth)
        {
            return;
        }

        if (!player.TryRestoreHealthServer(_healAmount))
        {
            return;
        }

        _collected = true;
        _pickupManager?.NotifyPickupCollected(_spawnPointIndex);

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
