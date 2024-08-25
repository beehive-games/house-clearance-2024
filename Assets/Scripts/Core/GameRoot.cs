using Character.Player;
using UnityEngine;

namespace BeehiveGames.HouseClearance
{
    public static class GameRoot
    {
        private static CoroutineRunner _internalCoroutineRunner;
        public static CoroutineRunner CoroutineRunner
        {
            get
            {
                if (_internalCoroutineRunner == null)
                {
                    var go = new GameObject("CoroutineRunner");
                    _internalCoroutineRunner = go.AddComponent<CoroutineRunner>();
                    Object.DontDestroyOnLoad(go);
                }
                return _internalCoroutineRunner;
            }
            
        }
        public static PlayerCharacter Player { private set; get; }

        public static void RegisterPlayer(PlayerCharacter character) => Player = character;
        public static void DeregisterPlayer() => Player = null;
    }
}
