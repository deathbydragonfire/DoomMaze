using UnityEngine;

public class AmbientDustController : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _offset = Vector3.zero;

    private void LateUpdate()
    {
        if (_target == null)
        {
            Debug.LogWarning("[AmbientDustController] _target is not assigned.", this);
            return;
        }

        transform.position = _target.position + _offset;
    }
}
