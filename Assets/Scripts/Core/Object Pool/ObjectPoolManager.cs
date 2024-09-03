using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace CoxlinCore
{
    public sealed class ObjectPoolManager : MonoBehaviour
    {
        public static ObjectPoolManager Instance { private set; get; }

        [SerializeField]
        private Transform _parent;

        private PooledObjectData[] _pooledObjects;

        private readonly HashSet<PooledObject> _objectsInUse = new HashSet<PooledObject>();
        private Dictionary<string, PooledObject[]> _pooledObjectDic = new Dictionary<string, PooledObject[]>();

        private void Awake()
        {
            Instance = this;
        }

        public void Init(PooledObjectData[] pooledObjects)
        {
            _pooledObjects = pooledObjects;
            _objectsInUse.Clear();
            _pooledObjectDic.Clear();
        }

        public void CreateObjectsFromPool(System.Action onCreateAction = null)
        {
            StartCoroutine(CreateObjectsCoroutine());
        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public IEnumerator CreateObjectsCoroutine(
            System.Action onCreateAction = null)
        {
            int count = _pooledObjects.Length;
            for (int i = 0; i < count; ++i)
            {
                onCreateAction?.Invoke();
                var p = _pooledObjects[i];
                Debug.Log("Pooling" +  p.Name);
                _pooledObjectDic[p.Name] = new PooledObject[p.Count];

                for (int j = 0; j < p.Count; ++j)
                {
                    var pooledObject = Instantiate(p.PooledObject, _parent);
                    pooledObject.transform.localPosition = Vector2.zero;
                    pooledObject.gameObject.SetActive(false);
                    _pooledObjectDic[p.Name][j] = pooledObject;
                }
                yield return null;
            }
        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public PooledObject Acquire(
            string name,
            Vector3 position,
            bool activateOnAcquire = true)
        {
            PooledObject pooledObject = null;
            UnityEngine.Assertions.Assert.IsTrue(_pooledObjectDic.ContainsKey(name), name + " is not in the object pool dic");
            var pooledObjs = _pooledObjectDic[name];
            if (_objectsInUse.ContainsAll(pooledObjs))
            {
                throw new UnityException("All objects for " + name + " are already in use");
            }
            else
            {
                int count = pooledObjs.Length;
                for (int i = 0; i < count; ++i)
                {
                    if (_objectsInUse.Contains(pooledObjs[i]))
                    {
                        continue;
                    }
                    else
                    {
                        pooledObject = pooledObjs[i];
                        _objectsInUse.Add(pooledObject);
                        pooledObject.transform.position = position;
                        pooledObject.gameObject.SetActive(activateOnAcquire);
                        pooledObject.OnAcquire();
                        break;
                    }
                }
            }
            UnityEngine.Assertions.Assert.IsNotNull(pooledObjs);
            return pooledObject;
        }

        public void Return(
            PooledObject pooledObj)
        {
            _objectsInUse.Remove(pooledObj);
            if (pooledObj == null)
            {
                return;
            }
            pooledObj.OnReturn();

            pooledObj.transform.position = _parent.position;
            pooledObj.gameObject.SetActive(false);
        }

        private void Update()
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (s.name != "Main")
            {
                var listToRet = new List<PooledObject>();
                foreach (var o in _objectsInUse)
                {
                    listToRet.Add(o);
                }
                listToRet.ForEach(x => Return(x));
            }
        }
    }
}
