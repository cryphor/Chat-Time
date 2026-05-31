using HarmonyLib;
using UnityEngine;

namespace ChatTimestamps
{
    /// <summary>
    /// Main mod class for Chat Timestamps.
    /// Adds timestamps to chat messages in Puck.
    /// </summary>
    public class ChatTimestampsMod : IPuckPlugin
    {
        private Harmony _harmony;

        /// <summary>
        /// Called when the mod is enabled by the mod manager.
        /// </summary>
        public bool OnEnable()
        {
            try
            {
                _harmony = new Harmony("chattimestamps");
                _harmony.PatchAll();
                Debug.Log("[ChatTimestamps] v1.0.0 loaded successfully!");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ChatTimestamps] Failed to load: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Called when the mod is disabled by the mod manager.
        /// </summary>
        public bool OnDisable()
        {
            try
            {
                if (_harmony != null)
                {
                    _harmony.UnpatchSelf();
                    _harmony = null;
                    Debug.Log("[ChatTimestamps] unloaded.");
                }
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ChatTimestamps] Failed to unload: {ex}");
                return false;
            }
        }
    }
}
