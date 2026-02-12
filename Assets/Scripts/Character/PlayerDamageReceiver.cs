using UnityEngine;

namespace Vhalor.Character
{
    [DisallowMultipleComponent]
    public sealed class PlayerDamageReceiver : MonoBehaviour, IDamageable
    {
        [SerializeField] private PlayerBlock _block;
        [SerializeField] private int _hp = 10;

        private void Awake()
        {
            if (_block == null)
                _block = GetComponent<PlayerBlock>();
        }

        // Versão simples (sem origem) — considera desbloqueável
        public void TakeDamage(int amount)
        {
            _hp -= amount;
        }

        // Versão melhor (use no seu inimigo): passa origem + golpe pesado
        public void TakeHit(int amount, Vector3 attackOrigin, bool isHeavy)
        {
            float mul = 1f;
            bool blocked = _block != null && _block.TryBlock(attackOrigin, isHeavy, out mul);

            int finalDamage = blocked ? Mathf.RoundToInt(amount * mul) : amount;
            _hp -= finalDamage;
        }
    }
}
