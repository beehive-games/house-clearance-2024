using UnityEngine;
using UnityEngine.UIElements;

namespace BeehiveGames.HouseClearance.Player
{
    public class CameraMovement : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _smoothing;
        [SerializeField] private Vector3 _offset;

        private Vector3 _initialTargetPosition;
        private Vector3 _initialPosition;
        private Vector3 _initialEulerAngles;
        private float _distance;
        private GameObject _tempObject;
        private Quaternion internalRotation;
        private Quaternion _initialRotationOffset;

        private void Start()
        {
            _initialTargetPosition = _target.position + _offset;
            _initialPosition = transform.position;
            _distance = Vector3.Distance(_initialTargetPosition, _initialPosition);
            _tempObject = new GameObject("CameraMoveTemp");
            _initialRotationOffset = Quaternion.Euler(transform.rotation.eulerAngles);
        }

        private void FixedUpdate()
        {
            transform.position = _target.position - _target.forward * _distance;
            _tempObject.transform.position = transform.position;
    
            // Calculate the desired rotation based on the target's forward direction
            var targetRotation = Quaternion.LookRotation(_target.forward, Vector3.up);
    
            // Apply the initial rotation offset
            targetRotation *= _initialRotationOffset;
    
            _tempObject.transform.rotation = targetRotation;
    
            transform.position = Vector3.Lerp(transform.position, _tempObject.transform.position, _smoothing.x * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, _tempObject.transform.rotation, _smoothing.y * Time.fixedDeltaTime);
        }


    }
}
