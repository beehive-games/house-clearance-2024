using UnityEngine;

namespace Utils
{
    public static class Rigidbody3dUtils
    {
        public static void SetVelocityX(this Rigidbody rb, float x)
        {
            rb.velocity = new Vector3(x, rb.velocity.y, rb.velocity.z);
        }

        public static void SetVelocityY(this Rigidbody rb, float y)
        {
            rb.velocity = new Vector3(rb.velocity.y, y, rb.velocity.z);
        }
        
        public static void SetVelocityZ(this Rigidbody rb, float z)
        {
            rb.velocity = new Vector3(rb.velocity.y, rb.velocity.y, z);
        }

        public static void SetVelocityFromDirection(this Rigidbody rb, float v, Vector3 direction,
            bool normalize = true)
        {
            if (normalize)
            {
                direction = Vector3.Normalize(direction);
            }
            
            rb.velocity = direction * v;
        }

        public static float VelocityX(this Rigidbody rb)
        {
            return rb.velocity.x;
        }
        
        public static float VelocityY(this Rigidbody rb)
        {
            return rb.velocity.y;
        }
        
        public static float VelocityZ(this Rigidbody rb)
        {
            return rb.velocity.y;
        }
    }
}