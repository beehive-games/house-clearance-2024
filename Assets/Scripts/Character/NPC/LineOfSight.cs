using UnityEngine;

namespace Character.NPC
{
    public class LineOfSight : MonoBehaviour
    {
    
        private SpriteRenderer _spriteRenderer;
        void Awake()
        {
            _spriteRenderer = GetComponentInParent<SpriteRenderer>();
        }

        public Vector3 GetLookDirection()
        {
            return transform.right * (_spriteRenderer.flipX ? -1 : 1);
        }
    
    }
}
