using UnityEngine;
using UnityEngine.InputSystem;

namespace Vhalor.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterLocomotion : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputActionReference _move;          // Player/Move (Vector2)
        [SerializeField] private Transform _cameraTransform;          // se vazio, usa Camera.main
        [SerializeField] private PlayerCombatCombo _combat;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float _maxSpeed = 6.5f;
        [SerializeField, Min(0.01f)] private float _accelTime = 0.08f;
        [SerializeField, Min(0.01f)] private float _decelTime = 0.10f;
        [SerializeField] private PlayerBlock _block;

        [Header("Rotation")]
        [SerializeField] private bool _rotateTowardsMove = true;
        [SerializeField, Min(0.01f)] private float _rotationSmoothTime = 0.08f;

        [Header("Gravity")]
        [SerializeField] private float _gravity = -25f;
        [SerializeField] private float _groundedStickForce = -2f;

        [Header("Mode")]
        [SerializeField] private bool _cameraRelativeMovement = true;

        [Header("Animation")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _speedParam = "Speed";
        [SerializeField, Min(0f)] private float _animSpeedDamp = 0.10f;

        private int _speedHash;
        private CharacterController _cc;

        private Vector3 _planarVel;
        private Vector3 _planarVelRef;
        private float _yawVelRef;
        private float _verticalVel;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            // ✅ auto-wire: evita estar apontando para o combat errado/nulo
            if (_combat == null)
                _combat = GetComponent<PlayerCombatCombo>();

            _speedHash = Animator.StringToHash(_speedParam);

            if (_block == null)
                _block = GetComponent<PlayerBlock>();
        }

        private void OnEnable()
        {
            if (_move?.action != null && !_move.action.enabled)
                _move.action.Enable();
        }

        private void OnDisable()
        {
            if (_move?.action != null && _move.action.enabled)
                _move.action.Disable();
        }

        private void Update()
        {
            if (_move?.action == null)
                return;

            float dt = Time.deltaTime;

            // 1) Input
            Vector2 input = _move.action.ReadValue<Vector2>();

            bool lockMove = _combat != null && _combat.BlockMovementInput;

            // 2) Direção desejada
            Vector3 desiredDir = lockMove ? Vector3.zero : GetDesiredDirection(input);

            // 3) Velocidade planar
            if (lockMove)
            {
                // Souls-like: para imediatamente e não acumula inércia
                _planarVel = Vector3.zero;
                _planarVelRef = Vector3.zero;
            }
            else
            {
                Vector3 targetVel = desiredDir * _maxSpeed;
                float smoothTime = (desiredDir.sqrMagnitude > 0.0001f) ? _accelTime : _decelTime;
                _planarVel = Vector3.SmoothDamp(_planarVel, targetVel, ref _planarVelRef, smoothTime, Mathf.Infinity, dt);
                float speedMul = _block != null ? _block.MoveSpeedMultiplier : 1f;
            }

            // 4) Gravidade
            if (_cc.isGrounded && _verticalVel < 0f)
                _verticalVel = _groundedStickForce;

            _verticalVel += _gravity * dt;

            // 5) Move
            Vector3 move = new Vector3(_planarVel.x, _verticalVel, _planarVel.z);
            _cc.Move(move * dt);

            // 6) Rotação só quando não está travado
            if (!lockMove && _rotateTowardsMove && _planarVel.sqrMagnitude > 0.01f)
            {
                float targetYaw = Mathf.Atan2(_planarVel.x, _planarVel.z) * Mathf.Rad2Deg;
                float newYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _yawVelRef, _rotationSmoothTime, Mathf.Infinity, dt);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }

            // 7) Animator Speed
            if (_animator != null)
            {
                float speed01 = Mathf.InverseLerp(0f, _maxSpeed, _planarVel.magnitude);
                _animator.SetFloat(_speedHash, speed01, _animSpeedDamp, dt);
            }
        }

        private Vector3 GetDesiredDirection(Vector2 input)
        {
            Vector3 v = new Vector3(input.x, 0f, input.y);
            v = Vector3.ClampMagnitude(v, 1f);

            if (!_cameraRelativeMovement || _cameraTransform == null)
                return v;

            Vector3 camFwd = _cameraTransform.forward;
            camFwd.y = 0f;
            camFwd.Normalize();

            Vector3 camRight = _cameraTransform.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector3 world = camRight * v.x + camFwd * v.z;
            return world.sqrMagnitude > 1f ? world.normalized : world;
        }
    }
}
