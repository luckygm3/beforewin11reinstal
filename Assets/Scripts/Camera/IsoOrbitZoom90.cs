using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace Vhalor.CameraSystem
{
    [DisallowMultipleComponent]
    public sealed class IsoOrbitZoom90 : MonoBehaviour
    {
        [Header("Cinemachine")]
        [SerializeField] private CinemachinePositionComposer _positionComposer;

        [Header("Input Actions (Input System)")]
        [SerializeField] private InputActionReference _zoom;        // Value Vector2  -> <Mouse>/scroll
        [SerializeField] private InputActionReference _rotateLeft;  // Button         -> Q
        [SerializeField] private InputActionReference _rotateRight; // Button         -> E

        [Header("Isometric Angle")]
        [SerializeField, Range(10f, 80f)] private float _pitch = 40f;

        [Header("Orbit (90°)")]
        [SerializeField] private float _stepDegrees = 90f;
        [SerializeField, Min(0.01f)] private float _rotationSmoothTime = 0.12f;

        [Header("Keep Diagonal Offset")]
        [Tooltip("Se ON, o offset base é o yaw atual ao iniciar (ex.: 45°).")]
        [SerializeField] private bool _useInitialYawAsBase = true;
        [Tooltip("Usado se _useInitialYawAsBase = false. Ex.: 45° para manter a diagonal.")]
        [SerializeField] private float _baseYawOffset = 45f;

        [Header("Zoom (Distance)")]
        [SerializeField] private float _minDistance = 6f;
        [SerializeField] private float _maxDistance = 18f;
        [SerializeField] private float _scrollSensitivity = 0.02f;
        [SerializeField, Min(0.01f)] private float _zoomSmoothTime = 0.08f;
        [SerializeField] private bool _invertScroll = false;

        private float _baseYaw;
        private int _stepIndex;
        private float _targetYaw;
        private float _yawVelocity;

        private float _targetDistance;
        private float _distanceVelocity;

        private void Reset()
        {
            _positionComposer = GetComponent<CinemachinePositionComposer>();
        }

        private void Awake()
        {
            if (_positionComposer == null)
                _positionComposer = GetComponent<CinemachinePositionComposer>();

            float startYaw = NormalizeAngle(transform.rotation.eulerAngles.y);

            _baseYaw = _useInitialYawAsBase
                ? startYaw
                : NormalizeAngle(_baseYawOffset);

            // Calcula qual step estamos (em relação ao baseYaw), para não “pular” ao primeiro giro
            _stepIndex = Mathf.RoundToInt(Mathf.DeltaAngle(_baseYaw, startYaw) / _stepDegrees);

            _targetYaw = NormalizeAngle(_baseYaw + _stepIndex * _stepDegrees);

            if (_positionComposer != null)
                _targetDistance = Mathf.Clamp(_positionComposer.CameraDistance, _minDistance, _maxDistance);
        }

        private void OnEnable()
        {
            EnableAction(_zoom);
            EnableAction(_rotateLeft);
            EnableAction(_rotateRight);

            if (_rotateLeft?.action != null)  _rotateLeft.action.performed += OnRotateLeft;
            if (_rotateRight?.action != null) _rotateRight.action.performed += OnRotateRight;
        }

        private void OnDisable()
        {
            if (_rotateLeft?.action != null)  _rotateLeft.action.performed -= OnRotateLeft;
            if (_rotateRight?.action != null) _rotateRight.action.performed -= OnRotateRight;

            DisableAction(_zoom);
            DisableAction(_rotateLeft);
            DisableAction(_rotateRight);
        }

        private void Update()
        {
            if (_positionComposer == null || _zoom?.action == null)
                return;

            float scrollY = _zoom.action.ReadValue<Vector2>().y;
            if (Mathf.Abs(scrollY) > 0.0001f)
            {
                float dir = _invertScroll ? 1f : -1f;
                _targetDistance = Mathf.Clamp(
                    _targetDistance + (scrollY * _scrollSensitivity * dir),
                    _minDistance,
                    _maxDistance
                );
            }
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            // Orbit suave mantendo sempre baseYaw + N*90
            float currentYaw = transform.rotation.eulerAngles.y;
            float smoothYaw = Mathf.SmoothDampAngle(currentYaw, _targetYaw, ref _yawVelocity, _rotationSmoothTime, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Euler(_pitch, smoothYaw, 0f);

            // Zoom suave
            if (_positionComposer != null)
            {
                float currentDist = _positionComposer.CameraDistance;
                float smoothDist = Mathf.SmoothDamp(currentDist, _targetDistance, ref _distanceVelocity, _zoomSmoothTime, Mathf.Infinity, dt);
                _positionComposer.CameraDistance = smoothDist;
            }
        }

        private void OnRotateLeft(InputAction.CallbackContext _)  => Step(-1);
        private void OnRotateRight(InputAction.CallbackContext _) => Step(+1);

        private void Step(int dir)
        {
            _stepIndex += dir;
            _targetYaw = NormalizeAngle(_baseYaw + _stepIndex * _stepDegrees);
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle < 0f) angle += 360f;
            return angle;
        }

        private static void EnableAction(InputActionReference reference)
        {
            if (reference?.action != null && !reference.action.enabled)
                reference.action.Enable();
        }

        private static void DisableAction(InputActionReference reference)
        {
            if (reference?.action != null && reference.action.enabled)
                reference.action.Disable();
        }
    }
}
