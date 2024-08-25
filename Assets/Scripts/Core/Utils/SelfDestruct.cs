using System.Collections;
using UnityEngine;

namespace Utils
{
    public class SelfDestruct : MonoBehaviour
    {

        [SerializeReference] private bool destroyOnStart = false;
        public float lifetime = 3f;

        public void StartSelfDestruct()
        {
            StartCoroutine(SelfDestructCO());
        }

        private IEnumerator SelfDestructCO()
        {
            yield return new WaitForSeconds(lifetime);
            Destroy(gameObject);
        }

        void Start()
        {
            if (destroyOnStart)
            {
                StartCoroutine(SelfDestructCO());
            }
        }

    }
}
