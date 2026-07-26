using UnityEngine;
using Category5.Core;
using Category5.Enemies;
using Category5.Boss;

namespace Category5.WeakPoints
{
    // static utility for routing damage through weak points
    // called by playercombat, networkedprojectile, and playerabilitymanager
    public static class WeakPointHelper
    {
        // type 1 (ranged): checks if a hit collider is a weak point
        // returns true if a weak point intercepted the damage (caller should skip normal damage)
        public static bool TryRouteRangedDamage(Collider hitCollider, int damage, ulong attackerClientId)
        {
            if (hitCollider == null) return false;

            // check the collider itself for a weak point component
            WeakPoint wp = hitCollider.GetComponent<WeakPoint>();
            if (wp == null)
            {
                // also check parents in case the collider is on a child of the weak point object
                wp = hitCollider.GetComponentInParent<WeakPoint>();
            }

            if (wp == null) return false;
            if (!wp.IsActive.Value) return false;

            wp.TakeDamage(damage, attackerClientId);
            return true;
        }

        // type 2 (melee zone): finds the enemy from a hit collider, then checks
        // if the attacker is standing inside any active melee zone weak point on that enemy
        // returns true if a weak point intercepted the damage
        public static bool TryRouteMeleeDamage(
            Collider hitCollider,
            int damage,
            ulong attackerClientId,
            Vector3 attackerPosition)
        {
            if (hitCollider == null) return false;

            // find the enemy or boss from the hit collider
            IWeakPointHost host = hitCollider.GetComponentInParent<EnemyBase>();
            if (host == null)
            {
                host = hitCollider.GetComponentInParent<BossBase>();
            }

            if (host == null) return false;

            // check if attacker is inside any active melee zone on this host
            WeakPoint zone = FindMeleeZoneAtPosition(host, attackerPosition);
            if (zone == null) return false;

            zone.TakeDamage(damage, attackerClientId);
            return true;
        }

        // finds the first active melee zone weak point that contains the given position
        public static WeakPoint FindMeleeZoneAtPosition(IWeakPointHost host, Vector3 position)
        {
            MonoBehaviour hostMono = host as MonoBehaviour;
            if (hostMono == null) return null;

            // get all weak points on the host (they are children)
            WeakPoint[] weakPoints = hostMono.GetComponentsInChildren<WeakPoint>(true);
            for (int i = 0; i < weakPoints.Length; i++)
            {
                if (weakPoints[i].Type == WeakPointType.MeleeZone
                    && weakPoints[i].IsActive.Value
                    && weakPoints[i].IsInsideZone(position))
                {
                    return weakPoints[i];
                }
            }

            return null;
        }

        // finds all weak points on a host entity
        public static WeakPoint[] GetWeakPoints(MonoBehaviour host)
        {
            if (host == null) return System.Array.Empty<WeakPoint>();
            return host.GetComponentsInChildren<WeakPoint>(true);
        }

        // finds a specific weak point by id on a host entity
        public static WeakPoint GetWeakPointById(MonoBehaviour host, string id)
        {
            if (host == null || string.IsNullOrEmpty(id)) return null;

            WeakPoint[] weakPoints = host.GetComponentsInChildren<WeakPoint>(true);
            for (int i = 0; i < weakPoints.Length; i++)
            {
                if (weakPoints[i].WeakPointId == id)
                {
                    return weakPoints[i];
                }
            }

            return null;
        }
    }
}
