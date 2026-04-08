using UnityEngine;
using DG.Tweening;



public class UICyclone : MonoBehaviour
{
    [SerializeField] private float rotationDuration = 20f; // Duration for one full rotation

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transform.DORotate(new Vector3(0, 0, -360), rotationDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)  // Ensures constant speed without slowing down
            .SetLoops(-1, LoopType.Restart) // -1 loops infinitely
            .SetRelative(); // Rotates "by" 360 degrees rather than "to" a fixed angle
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
