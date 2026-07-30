using UnityEngine;

public class MovingObject : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private GameObject[] movePoints;
    private int _currentPointIndex = 0;
    private int _direction = 1;
    
    private void Update()
    {
        if (movePoints.Length == 0) return;

        Transform targetPoint = movePoints[_currentPointIndex].transform;
        Vector3 direction = (targetPoint.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        if (Vector3.Distance(transform.position, targetPoint.position) < 0.1f)
        {
            if (_currentPointIndex == movePoints.Length - 1)
                _direction = -1;
            else if (_currentPointIndex == 0)
                _direction = 1;

            _currentPointIndex += _direction;
        }
    }
}
