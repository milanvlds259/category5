using UnityEngine;
using UnityEngine.Scripting;
using Category5.Enemies;

namespace Category5.WeakPoints
{
    // relays animation events from the enemy model animator to weak points on the root
    // place this on the enemy model's animator gameobject
    // add animation events to boss/enemy attack clips with function names like ActivateWeakPoint
    [Preserve]
    public class EnemyAnimationEventRelay : MonoBehaviour
    {
        private EnemyBase _enemy;

        public void Configure(EnemyBase enemy)
        {
            _enemy = enemy;
        }

        // activates a weak point by index (order in the weak point array)
        [Preserve]
        public void ActivateWeakPoint(int index)
        {
            if (_enemy == null)
                _enemy = GetComponentInParent<EnemyBase>();
            if (_enemy == null) return;

            var weakPoints = _enemy.GetComponentsInChildren<WeakPoint>(true);
            if (index >= 0 && index < weakPoints.Length)
            {
                weakPoints[index].Activate();
            }
        }

        // deactivates a weak point by index
        [Preserve]
        public void DeactivateWeakPoint(int index)
        {
            if (_enemy == null)
                _enemy = GetComponentInParent<EnemyBase>();
            if (_enemy == null) return;

            var weakPoints = _enemy.GetComponentsInChildren<WeakPoint>(true);
            if (index >= 0 && index < weakPoints.Length)
            {
                weakPoints[index].Deactivate();
            }
        }

        // activates a weak point by its unique id
        [Preserve]
        public void ActivateWeakPointByName(string id)
        {
            if (_enemy == null)
                _enemy = GetComponentInParent<EnemyBase>();
            if (_enemy == null) return;

            var wp = WeakPointHelper.GetWeakPointById(_enemy, id);
            if (wp != null) wp.Activate();
        }

        // deactivates a weak point by its unique id
        [Preserve]
        public void DeactivateWeakPointByName(string id)
        {
            if (_enemy == null)
                _enemy = GetComponentInParent<EnemyBase>();
            if (_enemy == null) return;

            var wp = WeakPointHelper.GetWeakPointById(_enemy, id);
            if (wp != null) wp.Deactivate();
        }

        // activates all weak points on this enemy
        [Preserve]
        public void ActivateAllWeakPoints()
        {
            if (_enemy == null)
                _enemy = GetComponentInParent<EnemyBase>();
            if (_enemy == null) return;

            var weakPoints = _enemy.GetComponentsInChildren<WeakPoint>(true);
            for (int i = 0; i < weakPoints.Length; i++)
            {
                weakPoints[i].Activate();
            }
        }

        // deactivates all weak points on this enemy
        [Preserve]
        public void DeactivateAllWeakPoints()
        {
            if (_enemy == null)
                _enemy = GetComponentInParent<EnemyBase>();
            if (_enemy == null) return;

            var weakPoints = _enemy.GetComponentsInChildren<WeakPoint>(true);
            for (int i = 0; i < weakPoints.Length; i++)
            {
                weakPoints[i].Deactivate();
            }
        }
    }
}
