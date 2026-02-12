using UnityEngine;

namespace Vhalor.Character
{
    [DisallowMultipleComponent]
    public sealed class CombatAnimEventProxy : MonoBehaviour
    {
        [SerializeField] private PlayerCombatCombo _combat;

        public void AnimEvent_SetComboStep(int step) => _combat.AnimEvent_SetComboStep(step);
        public void AnimEvent_OpenComboWindow() => _combat.AnimEvent_OpenComboWindow();
        public void AnimEvent_CloseComboWindow() => _combat.AnimEvent_CloseComboWindow();
        public void AnimEvent_DoHit() => _combat.AnimEvent_DoHit();
        public void AnimEvent_AttackEnd() => _combat.AnimEvent_AttackEnd();
    }
}
