using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    public struct MoveData : IReplicateData
    {
        public MoveData(float horizontal, float vertical, float targetYaw)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            TargetYaw = targetYaw;
            _tick = 0;
        }

        public float Horizontal;
        public float Vertical;
        public float TargetYaw;

        private uint _tick;

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct ReconcileData : IReconcileData
    {
        public ReconcileData(Vector3 position, Quaternion rotation, float verticalVelocity, bool isAlive)
        {
            Position = position;
            Rotation = rotation;
            VerticalVelocity = verticalVelocity;
            IsAlive = isAlive;
            _tick = 0;
        }

        public Vector3 Position;
        public Quaternion Rotation;
        public float VerticalVelocity;
        public bool IsAlive;

        private uint _tick;

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 12f;
    [SerializeField] private float _gravity = -25f;
    [SerializeField] private bool _useCameraRelativeMovement = false;
    [SerializeField] private bool _usePrediction = true;

    private NetworkTransform _networkTransform;
    private Rigidbody _rigidbody;
    private float _verticalVelocity;
    private MoveData _fallbackMoveData;
    private bool _loggedPredictionInitialized;
    private bool _loggedFirstOwnerInput;
    private bool _loggedFirstServerMove;
    private bool _loggedFirstServerReconcile;
    private bool _loggedMissingController;
    private bool _loggedDeadBlock;
    private bool _loggedFallbackMode;

    private const float ReconcileDistanceLogThreshold = 1f;

    private void Awake()
    {
        if (_playerNetwork == null)
            _playerNetwork = GetComponent<PlayerNetwork>();

        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        _networkTransform = GetComponent<NetworkTransform>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    public override void OnStartNetwork()
    {
        if (base.TimeManager != null)
            base.TimeManager.OnTick += TimeManager_OnTick;

        if (_networkTransform == null)
            _networkTransform = GetComponent<NetworkTransform>();

        if (_usePrediction && _networkTransform != null)
        {
            _networkTransform.enabled = false;
        }
        else if (!_usePrediction && _networkTransform != null)
        {
            _networkTransform.enabled = true;
        }

        if (!_loggedPredictionInitialized)
        {
            _loggedPredictionInitialized = true;
            Debug.Log(
                $"PlayerMovement OnStartNetwork movement prediction initialized object={name} usePrediction={_usePrediction} " +
                $"IsOwner={base.Owner.IsLocalClient} IsClient={base.IsClientInitialized} IsServer={base.IsServerInitialized} " +
                $"hasCharacterController={_characterController != null} hasNetworkTransform={_networkTransform != null}");
        }
    }

    public override void OnStopNetwork()
    {
        if (base.TimeManager != null)
            base.TimeManager.OnTick -= TimeManager_OnTick;

        if (_networkTransform != null)
            _networkTransform.enabled = true;
    }

    public void ResetMotionStateServer()
    {
        if (!base.IsServerInitialized)
            return;

        ResetLocalMotionState();
        ResetServerPhysicsState();
    }

    private void TimeManager_OnTick()
    {
        if (!CanMove())
        {
            HandleFrozenTick();
            return;
        }

        if (_usePrediction)
        {
            MoveData moveData = base.IsOwner ? BuildMoveData() : default;
            Replicate(moveData);

            if (base.IsServerInitialized)
                CreateReconcile();

            return;
        }

        RunServerAuthoritativeFallbackTick();
    }

    private void HandleFrozenTick()
    {
        if (base.IsOwner)
            ResetLocalMotionState();

        if (base.IsServerInitialized)
        {
            ResetServerPhysicsState();
            CreateReconcile();
        }
    }

    private static bool CanMove()
    {
        return GameStateManager.AllowsGameplay();
    }

    private void ResetLocalMotionState()
    {
        _verticalVelocity = 0f;
        _fallbackMoveData = default;
    }

    private void ResetServerPhysicsState()
    {
        if (!base.IsServerInitialized)
            return;

        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_rigidbody == null)
            return;

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    private MoveData BuildMoveData()
    {
        if (!CanMove())
            return default;

        if (Keyboard.current == null)
            return default;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            horizontal -= 1f;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            horizontal += 1f;

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            vertical -= 1f;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            vertical += 1f;

        Vector3 worldMoveDirection = GetWorldMoveDirection(horizontal, vertical);
        float targetYaw = transform.eulerAngles.y;

        if (worldMoveDirection.sqrMagnitude > 0.001f)
            targetYaw = Quaternion.LookRotation(worldMoveDirection, Vector3.up).eulerAngles.y;

        if (!_loggedFirstOwnerInput && new Vector2(horizontal, vertical).sqrMagnitude > 0.001f)
        {
            _loggedFirstOwnerInput = true;
            Debug.Log(
                $"PlayerMovement first owner input object={name} horizontal={horizontal:F2} vertical={vertical:F2} " +
                $"targetYaw={targetYaw:F1} localTick={base.TimeManager.LocalTick}");
        }

        return new MoveData(horizontal, vertical, targetYaw);
    }

    private Vector3 GetWorldMoveDirection(float horizontal, float vertical)
    {
        Vector3 moveDirection = new(horizontal, 0f, vertical);

        if (_useCameraRelativeMovement && Camera.main != null)
        {
            Vector3 cameraForward = Camera.main.transform.forward;
            Vector3 cameraRight = Camera.main.transform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            moveDirection = (cameraForward * vertical) + (cameraRight * horizontal);
        }

        return Vector3.ClampMagnitude(moveDirection, 1f);
    }

    [Replicate]
    private void Replicate(MoveData moveData, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        if (!CanMove())
        {
            ResetLocalMotionState();
            if (base.IsServerInitialized)
                ResetServerPhysicsState();
            return;
        }

        RunMovement(moveData, (float)base.TimeManager.TickDelta, logAsServerMove: base.IsServerInitialized && state.ContainsTicked());
    }

    [Reconcile]
    private void Reconcile(ReconcileData reconcileData, Channel channel = Channel.Unreliable)
    {
        if (_characterController == null)
        {
            LogMissingCharacterController("reconcile");
            return;
        }

        float correctionDistance = Vector3.Distance(transform.position, reconcileData.Position);
        if (base.IsOwner && correctionDistance > ReconcileDistanceLogThreshold)
        {
            Debug.Log(
                $"PlayerMovement respawn reconcile correction object={name} distance={correctionDistance:F3} " +
                $"from={transform.position} to={reconcileData.Position} isAlive={reconcileData.IsAlive}");
        }

        bool hadController = _characterController.enabled;
        if (hadController)
            _characterController.enabled = false;

        transform.SetPositionAndRotation(reconcileData.Position, reconcileData.Rotation);
        _verticalVelocity = reconcileData.VerticalVelocity;

        if (hadController)
            _characterController.enabled = true;

        if (base.IsServerInitialized && !_loggedFirstServerReconcile)
        {
            _loggedFirstServerReconcile = true;
            Debug.Log(
                $"PlayerMovement first server reconcile object={name} position={reconcileData.Position} " +
                $"rotation={reconcileData.Rotation.eulerAngles} verticalVelocity={reconcileData.VerticalVelocity:F3} isAlive={reconcileData.IsAlive}");
        }
    }

    private ReconcileData BuildReconcileData()
    {
        bool isAlive = _playerNetwork == null || _playerNetwork.IsAlive;
        return new ReconcileData(transform.position, transform.rotation, _verticalVelocity, isAlive);
    }

    public override void CreateReconcile()
    {
        Reconcile(BuildReconcileData());
    }

    private void RunServerAuthoritativeFallbackTick()
    {
        if (!CanMove())
            return;

        if (!_loggedFallbackMode)
        {
            _loggedFallbackMode = true;
            Debug.Log($"PlayerMovement fallback server-authoritative mode active on object={name}. Prediction is disabled.");
        }

        if (base.IsOwner)
        {
            MoveData moveData = BuildMoveData();

            if (base.IsServerInitialized)
            {
                _fallbackMoveData = moveData;
            }
            else
            {
                SubmitFallbackMovementServerRpc(moveData.Horizontal, moveData.Vertical, moveData.TargetYaw);
            }
        }

        if (base.IsServerInitialized)
            RunMovement(_fallbackMoveData, (float)base.TimeManager.TickDelta, logAsServerMove: true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitFallbackMovementServerRpc(float horizontal, float vertical, float targetYaw, NetworkConnection sender = null)
    {
        if (!CanMove())
            return;

        if (sender != Owner)
        {
            Debug.LogWarning($"PlayerMovement fallback movement rejected object={name} sender={sender} owner={Owner}");
            return;
        }

        _fallbackMoveData = new MoveData(horizontal, vertical, targetYaw);
    }

    private void RunMovement(MoveData moveData, float delta, bool logAsServerMove)
    {
        if (_characterController == null)
        {
            LogMissingCharacterController("movement");
            return;
        }

        if (_playerNetwork == null)
            _playerNetwork = GetComponent<PlayerNetwork>();

        if (!CanMove())
        {
            ResetLocalMotionState();
            if (base.IsServerInitialized)
                ResetServerPhysicsState();
            return;
        }

        if (_playerNetwork != null && !_playerNetwork.IsAlive)
        {
            if (!_loggedDeadBlock)
            {
                _loggedDeadBlock = true;
                Debug.Log($"PlayerMovement movement blocked because player is dead object={name} IsOwner={base.IsOwner} IsServer={base.IsServerInitialized}");
            }

            _verticalVelocity = 0f;
            return;
        }

        _loggedDeadBlock = false;

        if (!_characterController.enabled || !gameObject.activeInHierarchy)
            return;

        Vector2 planarInput = new(moveData.Horizontal, moveData.Vertical);
        float inputMagnitude = Mathf.Clamp01(planarInput.magnitude);
        Vector3 moveDirection = Vector3.zero;

        if (inputMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, moveData.TargetYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * delta);
            moveDirection = targetRotation * Vector3.forward * inputMagnitude;
        }

        if (_characterController.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += _gravity * delta;

        Vector3 velocity = moveDirection * _moveSpeed;
        velocity.y = _verticalVelocity;

        Vector3 beforePosition = transform.position;
        _characterController.Move(velocity * delta);
        Vector3 afterPosition = transform.position;

        if (logAsServerMove && !_loggedFirstServerMove && inputMagnitude > 0.001f)
        {
            _loggedFirstServerMove = true;
            Debug.Log(
                $"PlayerMovement server move object={name} before={beforePosition} after={afterPosition} " +
                $"velocity={velocity} tickDelta={delta:F4} usePrediction={_usePrediction}");
        }
    }

    private void LogMissingCharacterController(string context)
    {
        if (_loggedMissingController)
            return;

        _loggedMissingController = true;
        Debug.LogWarning($"PlayerMovement object={name} cannot run {context} because CharacterController == null", this);
    }
}
