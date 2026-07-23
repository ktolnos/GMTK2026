using UnityEngine;

namespace DefaultNamespace
{
    [CreateAssetMenu(fileName = "PlayerSprites", menuName = "Player Sprites", order = 0)]
    public class PlayerSprites : ScriptableObject
    {
        public SpriteAnimator.Animation idle;
    }
}