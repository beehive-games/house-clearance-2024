using Character.NPC;
using UnityEngine;

namespace Character
{
    public class MeleeZone : MonoBehaviour
    {

        public Enemy npcCharacter;
    
        void Start()
        {
            if (npcCharacter == null)
            {
                npcCharacter = transform.parent.gameObject.GetComponent<Enemy>();
            }
        }

    }
}
