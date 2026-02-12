using UnityEngine;
using UnityEngine.InputSystem;

namespace Vhalor.Character
{
    [DisallowMultipleComponent]
    public sealed class PlayerCombatCombo : MonoBehaviour
    {
        [System.Serializable]
        public struct AttackStep
        {
            public int damage;
            public float hitRadius;
            public float hitForwardOffset;
            public float hitHeight;
        }

        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private InputActionReference _attack; // Button
        [SerializeField] private InputActionReference _block;  // Button Hold (opcional)

        [Header("Animator Params")]
        [SerializeField] private string _comboStepInt = "ComboStep";
        [SerializeField] private string _attackTrigger = "Attack";
        [SerializeField] private string _nextAttackTrigger = "NextAttack";
        [SerializeField] private string _isBlockingBool = "IsBlocking";

        [Header("Combo")]
        [SerializeField] private AttackStep[] _steps = new AttackStep[3]
        {
            new AttackStep{ damage=1, hitRadius=0.35f, hitForwardOffset=0.90f, hitHeight=1.0f },
            new AttackStep{ damage=1, hitRadius=0.40f, hitForwardOffset=0.95f, hitHeight=1.0f },
            new AttackStep{ damage=2, hitRadius=0.45f, hitForwardOffset=1.05f, hitHeight=1.0f },
        };

        [Tooltip("Permite apertar antes da janela e ainda assim engatar (feeling Souls).")]
        [SerializeField, Min(0f)] private float _inputBufferTime = 0.15f;

        [Header("Hit Detection")]
        [SerializeField] private LayerMask _hittableLayers;

        [Header("Movement Lock")]
        [Tooltip("Bloqueia apenas input de movimento enquanto atacando.")]
        [SerializeField] private bool _lockMovementInputWhileAttacking = true;

        [Header("Animator Attack Tag (Movement Lock)")]
        [Tooltip("Tag dos states de ataque no Animator (ex.: Attack_1/2/3).")]
        [SerializeField] private string _attackTag = "Attack";
        [Tooltip("Layer do Animator onde os ataques estão (0 = Base Layer).")]
        [SerializeField] private int _attackLayerIndex = 0;

        [Header("Block")]
        [SerializeField] private bool _allowBlockWhileAttacking = false;

        public bool IsBlocking => _isBlocking;
        public bool IsAttacking => _isAttacking;

        /// <summary>
        /// Use isso na locomoção pra zerar apenas o input do WASD durante ataques.
        /// Agora é robusto: trava se _isAttacking OU se o Animator está em state taggeado como Attack.
        /// </summary>
        public bool BlockMovementInput =>
            _lockMovementInputWhileAttacking && (_isAttacking || IsAnimatorInAttackState());

        private int _comboStepHash;
        private int _attackHash;
        private int _nextAttackHash;
        private int _blockHash;
        private int _attackTagHash;

        private bool _isBlocking;
        private bool _isAttacking;

        private bool _comboWindowOpen;
        private bool _nextQueued;

        private int _currentStep;     // 1..N (setado por AnimEvent_SetComboStep)
        private float _bufferedUntil; // timestamp do buffer

        private readonly Collider[] _hits = new Collider[16];

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            _comboStepHash = Animator.StringToHash(_comboStepInt);
            _attackHash = Animator.StringToHash(_attackTrigger);
            _nextAttackHash = Animator.StringToHash(_nextAttackTrigger);
            _blockHash = Animator.StringToHash(_isBlockingBool);
            _attackTagHash = Animator.StringToHash(_attackTag);
        }

        private void OnEnable()
        {
            EnableAction(_attack);
            EnableAction(_block);

            if (_attack?.action != null)
                _attack.action.performed += OnAttack;

            if (_block?.action != null)
            {
                _block.action.performed += OnBlockOn;
                _block.action.canceled += OnBlockOff;
            }
        }

        private void OnDisable()
        {
            if (_attack?.action != null)
                _attack.action.performed -= OnAttack;

            if (_block?.action != null)
            {
                _block.action.performed -= OnBlockOn;
                _block.action.canceled -= OnBlockOff;
            }

            DisableAction(_attack);
            DisableAction(_block);
        }

        private void OnAttack(InputAction.CallbackContext _)
        {
            // Bufferiza o input (melhora muito o feel)
            _bufferedUntil = Time.time + _inputBufferTime;

            if (!_isAttacking)
            {
                StartCombo();
                return;
            }

            TryQueueNextFromBuffer();
        }

        private void StartCombo()
        {
            _isAttacking = true;
            _comboWindowOpen = false;
            _nextQueued = false;

            _currentStep = 1; // será confirmado por AnimEvent_SetComboStep(1)

            if (_animator != null)
            {
                _animator.SetInteger(_comboStepHash, 1);
                _animator.ResetTrigger(_nextAttackHash);
                _animator.SetTrigger(_attackHash);
            }

            if (_isBlocking && !_allowBlockWhileAttacking)
                SetBlocking(false);
        }

        private void TryQueueNextFromBuffer()
        {
            if (!_comboWindowOpen) return;
            if (_nextQueued) return;
            if (Time.time > _bufferedUntil) return;

            int next = _currentStep + 1;
            if (next > _steps.Length) return;

            _nextQueued = true;

            if (_animator != null)
            {
                _animator.SetInteger(_comboStepHash, next);
                _animator.SetTrigger(_nextAttackHash);
            }
        }

        private void OnBlockOn(InputAction.CallbackContext _)
        {
            if (_isAttacking && !_allowBlockWhileAttacking) return;
            SetBlocking(true);
        }

        private void OnBlockOff(InputAction.CallbackContext _)
        {
            SetBlocking(false);
        }

        private void SetBlocking(bool value)
        {
            _isBlocking = value;
            if (_animator != null)
                _animator.SetBool(_blockHash, _isBlocking);
        }

        // ==========================
        // Animation Events
        // ==========================

        public void AnimEvent_SetComboStep(int step)
        {
            _currentStep = Mathf.Clamp(step, 1, _steps.Length);

            // 🔥 evita vazamento de janela entre ataques
            _comboWindowOpen = false;

            _nextQueued = false;
        }

        public void AnimEvent_OpenComboWindow()
        {
            _comboWindowOpen = true;
            TryQueueNextFromBuffer(); // se apertou antes, engata agora
        }

        public void AnimEvent_CloseComboWindow()
        {
            _comboWindowOpen = false;
        }

        public void AnimEvent_DoHit()
        {
            DoHit(_currentStep);
        }

        public void AnimEvent_AttackEnd()
        {
            _isAttacking = false;
            _comboWindowOpen = false;
            _nextQueued = false;
            _currentStep = 0;
        }

        private bool IsAnimatorInAttackState()
        {
            if (_animator == null) return false;

            var current = _animator.GetCurrentAnimatorStateInfo(_attackLayerIndex);
            if (current.tagHash == _attackTagHash) return true;

            if (_animator.IsInTransition(_attackLayerIndex))
            {
                var next = _animator.GetNextAnimatorStateInfo(_attackLayerIndex);
                if (next.tagHash == _attackTagHash) return true;
            }

            return false;
        }

        private void DoHit(int step)
        {
            if (step < 1 || step > _steps.Length) return;

            var data = _steps[step - 1];

            Vector3 center = transform.position
                           + Vector3.up * data.hitHeight
                           + transform.forward * data.hitForwardOffset;

            int count = Physics.OverlapSphereNonAlloc(
                center,
                data.hitRadius,
                _hits,
                _hittableLayers,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                var col = _hits[i];
                if (col == null) continue;

                if (col.transform == transform)
                {
                    _hits[i] = null;
                    continue;
                }

                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(data.damage);

                _hits[i] = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_steps == null || _steps.Length == 0) return;

            int idx = Mathf.Clamp((_currentStep <= 0 ? 1 : _currentStep) - 1, 0, _steps.Length - 1);
            var data = _steps[idx];

            Vector3 center = transform.position
                           + Vector3.up * data.hitHeight
                           + transform.forward * data.hitForwardOffset;

            Gizmos.DrawWireSphere(center, data.hitRadius);
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
        public void CancelCombo()
        {
            _isAttacking = false;
            _comboWindowOpen = false;
            _nextQueued = false;
            _currentStep = 0;

            if (_animator != null)
            {
                _animator.ResetTrigger(_attackHash);
                _animator.ResetTrigger(_nextAttackHash);
                _animator.SetInteger(_comboStepHash, 0);
            }
        }
    }
}
