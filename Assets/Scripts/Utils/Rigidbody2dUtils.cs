using UnityEngine;

namespace Utils
{
    public static class Rigidbody2dUtils
    {
        public static void SetVelocityX(this Rigidbody2D rb, float x)
        {
            rb.velocity = new Vector2(x, rb.velocity.y);
        }

        public static void SetVelocityY(this Rigidbody2D rb, float y)
        {
            rb.velocity = new Vector2(rb.velocity.y, y);
        }

        public static float VelocityX(this Rigidbody2D rb)
        {
            return rb.velocity.x;
        }
        
        public static float VelocityY(this Rigidbody2D rb)
        {
            return rb.velocity.y;
        }
    }
}