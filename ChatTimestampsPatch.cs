using HarmonyLib;
using System;

namespace ChatTimestamps
{
    [HarmonyPatch(typeof(UIChat))]
    public static class ChatTimestampsPatch
    {
        /// <summary>
        /// Postfix on GetChatMessagePrefix — prepends a [HH:mm:ss] timestamp to every chat message.
        /// </summary>
        [HarmonyPatch("GetChatMessagePrefix")]
        [HarmonyPostfix]
        public static void Postfix_GetChatMessagePrefix(ref string __result)
        {
            if (string.IsNullOrWhiteSpace(__result))
                return;

            // Avoid double-timestamping (check if already prefixed with [HH:mm:ss])
            if (__result.StartsWith("[") && __result.Contains("]"))
                return;

            // Format: [HH:mm:ss]
            string timestamp = DateTime.Now.ToString("[HH:mm:ss] ");
            __result = timestamp + __result;
        }
    }
}