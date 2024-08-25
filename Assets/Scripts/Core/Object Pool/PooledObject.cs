using UnityEngine;

namespace CoxlinCore
{
    public abstract class PooledObject : MonoBehaviour
    {
        public bool IsActive { private set; get; }
        public virtual void OnAcquire()
        {
            IsActive = true;
        }
        public virtual void OnReturn()
        {
            IsActive = false;
        }
    }
}
