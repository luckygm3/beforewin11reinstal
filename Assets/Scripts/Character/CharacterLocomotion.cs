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

        [Header("Movement")]
        [SerializeField, Min(0f)] private float _maxSpeed = 6.5f;
        [SerializeField, Min(0.01f)] private float _accelTime = 0.08f;
        [SerializeField, Min(0.01f)] private float _decelTime = 0.10f;

        [Header("Rotation")]
        [SerializeField] private bool _rotateTowardsMove = true;
        [SerializeField, Min(0.01f)] private float _rotationSmoothTime = 0.08f;

        [Header("Gravity")]
        [SerializeField] private float _gravity = -25f;
        [SerializeField] private float _groundedStickForce = -2f;

        [Header("Mode")]
        [Tooltip("ON = WASD relativo ao yaw da câmera (W = cima da tela). OFF = mundo (W = +Z).")]
        [SerializeField] private bool _cameraRelativeMovement = true;

        [Header("Animation")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _speedParam = "Speed";
        [SerializeField, Min(0f)] private float _animSpeedDamp = 0.10f;

        private int _speedHash;

        private CharacterController _cc;

        private Vector3 _planarVel;          // velocidade XZ atual
        private Vector3 _planarVelRef;       // ref do SmoothDamp
        private float _yawVelRef;            // ref do SmoothDampAngle
        private float _verticalVel;          // gravidade

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            _speedHash = Animator.StringToHash(_speedParam);
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
            Vector3 desiredDir = GetDesiredDirection(input);

            // 2) Target velocity (acel/decel suave)
            Vector3 targetVel = desiredDir * _maxSpeed;
            float smoothTime = (desiredDir.sqrMagnitude > 0.0001f) ? _accelTime : _decelTime;
            _planarVel = Vector3.SmoothDamp(_planarVel, targetVel, ref _planarVelRef, smoothTime, Mathf.Infinity, dt);

            // 3) Gravidade (CharacterController)
            if (_cc.isGrounded && _verticalVel < 0f)
                _verticalVel = _groundedStickForce;

            _verticalVel += _gravity * dt;

            // 4) Movimento
            Vector3 move = new Vector3(_planarVel.x, _verticalVel, _planarVel.z);
            _cc.Move(move * dt);

            // 5) Rotação para direção do movimento (suave)
            if (_rotateTowardsMove && _planarVel.sqrMagnitude > 0.01f)
            {
                float targetYaw = Mathf.Atan2(_planarVel.x, _planarVel.z) * Mathf.Rad2Deg;
                float newYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _yawVelRef, _rotationSmoothTime, Mathf.Infinity, dt);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }   

            if (_animator != null)
            {
                // Normalizado 0..1 (recomendado pra BlendTree simples Idle/Run)
                float speed01 = Mathf.InverseLerp(0f, _maxSpeed, _planarVel.magnitude);

                _animator.SetFloat(_speedHash, speed01, _animSpeedDamp, Time.deltaTime);
            }
        }

        private Vector3 GetDesiredDirection(Vector2 input)
        {
            Vector3 v = new Vector3(input.x, 0f, input.y);
            v = Vector3.ClampMagnitude(v, 1f);

            if (!_cameraRelativeMovement || _cameraTransform == null)
                return v;

            // Camera-relative: usa yaw da câmera (projetado no chão)
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
