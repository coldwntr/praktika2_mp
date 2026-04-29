using FishNet.Object;
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
        if (!IsServerInitialized || _collected)
        {
            return;
        }

        PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
        if (player == null || player.IsDeadOrRespawning || player.HP >= player.MaxHealth)
        {
            return;
        }

        player.Heal(_healAmount);
        _collected = true;
        _pickupManager?.NotifyPickupCollected(_spawnPointIndex);

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            ServerManager.Despawn(NetworkObject);
        }
    }
}
