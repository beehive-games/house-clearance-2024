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
            PlayerCharacter playerCharacter = other.GetComponentInParent<PlayerCharacter>();
            if (playerCharacter == null) return;
            playerCharacter.InTowerCorner(this);
        }
        
        private void OnCollisionStay(Collision other)
        {
            CheckSetCorners(other.gameObject);
        }
        
        private void OnTriggerStay(Collider other)
        {
            CheckSetCorners(other.gameObject);
        }
    }
}
