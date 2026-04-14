using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 12f;
    [SerializeField] private float _gravity = -25f;
    [SerializeField] private float _sendInterval = 0.05f;
    [SerializeField] private bool _useCameraRelativeMovement = false;

    private Vector3 _serverMoveDirection;
    private Vector3 _lastSentMoveDirection;
    private float _nextSendTime;
    private float _verticalVelocity;

    private void Awake()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }
    }

    private void Update()
    {
        if (IsOwner)
        {
            HandleOwnerInput();
        }

        if (IsServer)
        {
            SimulateMovementServer();
        }
    }

    private void HandleOwnerInput()
    {
        Vector3 moveDirection = ReadMoveDirection();

        bool inputChanged = moveDirection != _lastSentMoveDirection;
        bool keepAliveSend = moveDirection != Vector3.zero && Time.unscaledTime >= _nextSendTime;

        if (!inputChanged && !keepAliveSend)
        {
            return;
        }

        _lastSentMoveDirection = moveDirection;
        _nextSendTime = Time.unscaledTime + _sendInterval;

        if (IsServer)
        {
            _serverMoveDirection = moveDirection;
            return;
        }

        SubmitMovementServerRpc(moveDirection);
    }

    private Vector3 ReadMoveDirection()
    {
        if (Keyboard.current == null)
        {
            return Vector3.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            horizontal += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            vertical -= 1f;
        }

        if (Keyboard.current.wKey.isPressed)
        {
            vertical += 1f;
        }

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical);

        if (_useCameraRelativeMovement && Camera.main != null)
        {
            Vector3 cameraForward = Camera.main.transform.forward;
            Vector3 cameraRight = Camera.main.transform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            cameraForward.Normalize();
            cameraRight.Normalize();

            moveDirection = cameraForward * vertical + cameraRight * horizontal;
        }

        return Vector3.ClampMagnitude(moveDirection, 1f);
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SubmitMovementServerRpc(Vector3 moveDirection)
    {
        moveDirection.y = 0f;
        _serverMoveDirection = Vector3.ClampMagnitude(moveDirection, 1f);
    }

    private void SimulateMovementServer()
    {
        if (_characterController == null || _playerNetwork == null)
        {
            return;
        }

        if (!_characterController.enabled || !gameObject.activeInHierarchy)
        {
            return;
        }

        Vector3 moveDirection = _playerNetwork.IsAlive.Value ? _serverMoveDirection : Vector3.zero;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }

        if (_characterController.isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -2f;
        }

        _verticalVelocity += _gravity * Time.deltaTime;

        Vector3 velocity = moveDirection * _moveSpeed;
        velocity.y = _verticalVelocity;

        _characterController.Move(velocity * Time.deltaTime);
    }
}
