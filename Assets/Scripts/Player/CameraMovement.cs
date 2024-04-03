using System.Xml;
using UnityEngine;

namespace Player
{
    public class CameraMovement : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _smoothing;
        [SerializeField] private Vector3 _offset;

        private float _startTime;
        private Vector3 _velocity = Vector3.zero;
        void Start()
        {
            _startTime = Time.time;
        }
        
        
        // do x on update, y on fixed
        void FixedUpdate()
        {
            Vector3 currentPos = transform.position;
            Vector3 targetPos = _target.position + _offset;
            if (targetPos.y == currentPos.y && currentPos.x == targetPos.x)
            {
                return;
            }
            float tY = (Time.time - _startTime) / _smoothing.y;
            float y = Mathf.SmoothDamp(currentPos.y, targetPos.y, ref _velocity.y, tY);
            float tX = (Time.time - _startTime) / _smoothing.x;
            float x = Mathf.SmoothDamp(currentPos.x, targetPos.x, ref _velocity.x, tX);
            transform.position = new Vector3(_smoothing.x <= 0f? targetPos.x : x, _smoothing.y <= 0f? targetPos.y : y, currentPos.z);
            
        }
        // Update is called once per frame
        void Update()
        {
            /*
            Vector3 currentPos = transform.position;
            Vector3 targetPos = _target.position + _offset;
            if (currentPos.x == targetPos.x)
            {
                return;
            }
            float tX = (Time.time - _startTime) / _smoothing.x;
            float x = Mathf.SmoothDamp(currentPos.x, targetPos.x, ref _velocity.x, tX);
            transform.position = new Vector3(_smoothing.x <= 0f? targetPos.x : x, currentPos.y, currentPos.z);
*/
        }
    }
}
