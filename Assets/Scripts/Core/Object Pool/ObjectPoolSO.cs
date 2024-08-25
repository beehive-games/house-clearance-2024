using UnityEngine;

namespace CoxlinCore
{
    [System.Serializable]
    public class PooledObjectData
    {
        public string Name;
        public PooledObject PooledObject;
        public int Count;
    }

    [CreateAssetMenu(menuName = "Object Pooling/Pool Data")]
    public class ObjectPoolSO : ScriptableObject
    {
        public PooledObjectData[] PooledObjects;
    }
}
