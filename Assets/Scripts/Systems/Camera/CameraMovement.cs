using UnityEngine;

namespace BeehiveGames.HouseClearance.Player
{
    public class CameraMovement : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _smoothing;
        [SerializeField] private Vector3 _offset;

        private Vector3 _initialTargetPosition;
        private Vector3 _initialPosition;
        private float _distance;
        private GameObject _tempObject;
        
        private void Start()
        {
            _initialTargetPosition = _target.position + _offset;
            _initialPosition = transform.position;
            _distance = Vector3.Distance(_initialTargetPosition, _initialPosition);
            _tempObject = new GameObject("CameraMoveTemp");
        }

        private void FixedUpdate()
        {
            transform.position = _target.position - _target.forward * _distance;
            _tempObject.transform.position = transform.position;
            _tempObject.transform.rotation = transform.rotation;
            var rotation = Quaternion.FromToRotation(transform.forward, _target.forward);
            _tempObject.transform.RotateAround(_target.position + _offset, Vector3.up, rotation.eulerAngles.y);
            transform.position = Vector3.Lerp(transform.position, _tempObject.transform.position, _smoothing.x * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, _tempObject.transform.rotation, _smoothing.y * Time.fixedDeltaTime);
        }
    }
}
