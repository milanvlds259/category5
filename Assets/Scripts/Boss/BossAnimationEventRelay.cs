using UnityEngine;
using UnityEngine.Scripting;
using Category5.Boss;

namespace Category5.WeakPoints
{
    // relays animation events from the boss model animator to weak points on the root
    // place this on the boss model's animator gameobject
    [Preserve]
    public class BossAnimationEventRelay : MonoBehaviour
    {
        private BossBase _boss;

        public void Configure(BossBase boss)
        {
            _boss = boss;
        }

        // activates a weak point by index (order in the weak point array)
        [Preserve]
        public void ActivateWeakPoint(int index)
        {
            if (_boss == null)
                _boss = GetComponentInParent<BossBase>();
            if (_boss == null) return;

            var weakPoints = _boss.GetComponentsInChildren<WeakPoint>(true);
            if (index >= 0 && index < weakPoints.Length)
            {
                weakPoints[index].Activate();
            }
        }

        // deactivates a weak point by index
        [Preserve]
        public void DeactivateWeakPoint(int index)
        {
            if (_boss == null)
                _boss = GetComponentInParent<BossBase>();
            if (_boss == null) return;

            var weakPoints = _boss.GetComponentsInChildren<WeakPoint>(true);
            if (index >= 0 && index < weakPoints.Length)
            {
                weakPoints[index].Deactivate();
            }
        }

        // activates a weak point by its unique id
        [Preserve]
        public void ActivateWeakPointByName(string id)
        {
            if (_boss == null)
                _boss = GetComponentInParent<BossBase>();
            if (_boss == null) return;

            var wp = WeakPointHelper.GetWeakPointById(_boss, id);
            if (wp != null) wp.Activate();
        }

        // deactivates a weak point by its unique id
        [Preserve]
        public void DeactivateWeakPointByName(string id)
        {
            if (_boss == null)
                _boss = GetComponentInParent<BossBase>();
            if (_boss == null) return;

            var wp = WeakPointHelper.GetWeakPointById(_boss, id);
            if (wp != null) wp.Deactivate();
        }

        // activates all weak points on this boss
        [Preserve]
        public void ActivateAllWeakPoints()
        {
            if (_boss == null)
                _boss = GetComponentInParent<BossBase>();
            if (_boss == null) return;

            var weakPoints = _boss.GetComponentsInChildren<WeakPoint>(true);
            for (int i = 0; i < weakPoints.Length; i++)
            {
                weakPoints[i].Activate();
            }
        }

        // deactivates all weak points on this boss
        [Preserve]
        public void DeactivateAllWeakPoints()
        {
            if (_boss == null)
                _boss = GetComponentInParent<BossBase>();
            if (_boss == null) return;

            var weakPoints = _boss.GetComponentsInChildren<WeakPoint>(true);
            for (int i = 0; i < weakPoints.Length; i++)
            {
                weakPoints[i].Deactivate();
            }
        }
    }
}
