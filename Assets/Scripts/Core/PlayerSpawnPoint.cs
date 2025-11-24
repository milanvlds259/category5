using UnityEngine;

namespace Category5.Core
{
    public class PlayerSpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }
    }
}
