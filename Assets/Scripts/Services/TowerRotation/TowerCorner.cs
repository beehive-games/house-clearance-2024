using System;
using Character.Player;
using UnityEngine;

namespace Environment
{
    public class TowerCorner : MonoBehaviour
    {
        public TowerCorners towerCorner;
        public float turnTime = 2f;

        private void CheckSetCorners(GameObject other)
        {
            CharacterBase character = other.GetComponentInParent<CharacterBase>();
            if (character == null) return;
            character.InTowerCorner(this);
        }
        
        private void OnCollisionStay(Collision other)
        {
            CheckSetCorners(other.gameObject);
        }
        
        private void OnTriggerStay(Collider other)
        {
            CheckSetCorners(other.gameObject);
        }
        
        private void OnTriggerExit(Collider other)
        {
            CharacterBase character = other.GetComponentInParent<CharacterBase>();
            if (character == null) return;
            character.OutTowerCorner();
        }
    }
}
