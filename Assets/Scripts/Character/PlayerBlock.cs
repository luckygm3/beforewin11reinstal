using UnityEngine;
using UnityEngine.InputSystem;

namespace Vhalor.Character
{
    [DisallowMultipleComponent]
    public sealed class PlayerBlock : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private InputActionReference _block;     // Button (hold)
        [SerializeField] private PlayerCombatCombo _combat;       // para quebrar combo ao bloquear

        [Header("Animator Params")]
        [SerializeField] private string _isBlockingBool = "IsBlocking";
        [SerializeField] private string _blockHitTrigger = "BlockHit";
        [SerializeField] private string _guardBreakTrigger = "GuardBreak";

        [Header("Block Rules")]
        [Tooltip("Se true, ao segurar block o combo/ataque atual é cancelado imediatamente.")]
        [SerializeField] private bool _blockCancelsAttacks = true;

        [Tooltip("Só bloqueia ataques que vêm da frente do personagem.")]
        [SerializeField] private bool _frontOnly = true;

        [Tooltip("Ângulo total do cone de bloqueio (ex.: 140 significa 70 pra cada lado).")]
        [SerializeField, Range(10f, 180f)] private float _blockConeAngle = 140f;

        [Tooltip("Multiplicador de dano quando bloqueia (0 = zera, 0.2 = toma 20%).")]
        [SerializeField, Range(0f, 1f)] private float _blockedDamageMultiplier = 0f;

        [Header("Movement (optional)")]
        [SerializeField, Range(0.1f, 1f)] private float _blockMoveMultiplier = 0.60f;

        public bool IsBlocking => _isBlocking;
        public float MoveSpeedMultiplier => _isBlocking ? _blockMoveMultiplier : 1f;

        private bool _isBlocking;

        private int _isBlockingHash;
        private int _blockHitHash;
        private int _guardBreakHash;

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            if (_combat == null)
                _combat = GetComponent<PlayerCombatCombo>();

            _isBlockingHash = Animator.StringToHash(_isBlockingBool);
            _blockHitHash = Animator.StringToHash(_blockHitTrigger);
            _guardBreakHash = Animator.StringToHash(_guardBreakTrigger);
        }

        private void OnEnable()
        {
            if (_block?.action != null && !_block.action.enabled)
                _block.action.Enable();

            if (_block?.action != null)
            {
                _block.action.performed += OnBlockPressed;
                _block.action.canceled += OnBlockReleased;
            }
        }

        private void OnDisable()
        {
            if (_block?.action != null)
            {
                _block.action.performed -= OnBlockPressed;
                _block.action.canceled -= OnBlockReleased;

                if (_block.action.enabled)
                    _block.action.Disable();
            }
        }

        private void OnBlockPressed(InputAction.CallbackContext _)
        {
            if (_blockCancelsAttacks && _combat != null && _combat.IsAttacking)
                _combat.CancelCombo(); // quebra combo/ataque atual

            SetBlocking(true);
        }

        private void OnBlockReleased(InputAction.CallbackContext _)
        {
            SetBlocking(false);
        }

        private void SetBlocking(bool value)
        {
            _isBlocking = value;
            if (_animator != null)
                _animator.SetBool(_isBlockingHash, _isBlocking);
        }

        /// <summary>
        /// Chame isso quando o Player receber um golpe. Retorna true se bloqueou.
        /// attackOrigin: posição de onde veio o ataque (inimigo).
        /// isHeavy: se for golpe pesado, pode dar guard break.
        /// </summary>
        public bool TryBlock(Vector3 attackOrigin, bool isHeavy, out float damageMultiplier)
        {
            damageMultiplier = 1f;

            if (!_isBlocking)
                return false;

            if (_frontOnly)
            {
                Vector3 toAttacker = attackOrigin - transform.position;
                toAttacker.y = 0f;

                if (toAttacker.sqrMagnitude > 0.0001f)
                {
                    float angle = Vector3.Angle(transform.forward, toAttacker.normalized);
                    if (angle > (_blockConeAngle * 0.5f))
                        return false; // veio por trás/lado => não bloqueia
                }
            }

            if (isHeavy)
            {
                // golpe pesado quebra a guarda
                if (_animator != null)
                    _animator.SetTrigger(_guardBreakHash);

                SetBlocking(false);
                return false; // não bloqueou (tomou o golpe normalmente)
            }

            // bloqueou com sucesso
            damageMultiplier = _blockedDamageMultiplier;

            if (_animator != null)
                _animator.SetTrigger(_blockHitHash);

            return true;
        }
    }
}
