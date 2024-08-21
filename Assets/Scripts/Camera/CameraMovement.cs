using UnityEngine;

namespace Player
{
    public class CameraMovement : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _smoothing;
        [SerializeField] private Vector3 _offset;

        private Vector3 initialTargetPosition;
        private Vector3 initialPosition;
        private float distance;
        
        
        void Start()
        {
            initialTargetPosition = _target.position + _offset;
            initialPosition = transform.position;
            distance = Vector3.Distance(initialTargetPosition, initialPosition);
        }

        void FixedUpdate()
        {
            transform.position = _target.position - _target.forward * distance;
           
            var temp = new GameObject()
            {
                transform =
                {
                    position = transform.position,
                    rotation = transform.rotation
                }
            };

            var rotation = Quaternion.FromToRotation(transform.forward, _target.forward);

            temp.transform.RotateAround(_target.position + _offset, Vector3.up, rotation.eulerAngles.y);

            //transform.position = temp.transform.position;
            //transform.rotation = temp.transform.rotation;
            
            transform.position = Vector3.Lerp(transform.position, temp.transform.position, _smoothing.x * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, temp.transform.rotation, _smoothing.y * Time.fixedDeltaTime);
            Destroy(temp);
        }
    }
}
