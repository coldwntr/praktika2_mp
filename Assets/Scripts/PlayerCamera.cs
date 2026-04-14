using Unity.Netcode;
using UnityEngine;

public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private Vector3 _followOffset = new Vector3(0f, 6f, -7f);
    [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float _followSmoothness = 12f;

    private Camera _targetCamera;

    public override void OnNetworkSpawn()
    {
        enabled = IsOwner;

        if (!IsOwner)
        {
            return;
        }

        AttachCamera();
    }

    private void LateUpdate()
    {
        if (!IsOwner)
        {
            return;
        }

        if (_targetCamera == null)
        {
            AttachCamera();

            if (_targetCamera == null)
            {
                return;
            }
        }

        Vector3 desiredPosition = transform.position + _followOffset;
        _targetCamera.transform.position = Vector3.Lerp(
            _targetCamera.transform.position,
            desiredPosition,
            _followSmoothness * Time.deltaTime);

        _targetCamera.transform.LookAt(transform.position + _lookOffset);
    }

    private void AttachCamera()
    {
        _targetCamera = Camera.main;
    }
}
