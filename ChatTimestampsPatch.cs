using HarmonyLib;
using System;

namespace ChatTimestamps
{
    [HarmonyPatch(typeof(UIChat))]
    public static class ChatTimestampsPatch
    {
        /// <summary>
        /// Postfix on GetChatMessagePrefix — inserts a [HH:mm:ss] timestamp next to the [TEAM] tag.
        /// Team chat:   [TEAM] [HH:mm:ss] PlayerName:
        /// All chat:    [HH:mm:ss] PlayerName:
        /// </summary>
        [HarmonyPatch("GetChatMessagePrefix")]
        [HarmonyPostfix]
        public static void Postfix_GetChatMessagePrefix(ChatMessage chatMessage, ref string __result)
        {
            if (string.IsNullOrWhiteSpace(__result))
                return;

            // Avoid double-timestamping
            if (__result.Contains("[HH:mm:ss]"))
                return;

            string timestamp = DateTime.Now.ToString("[HH:mm:ss] ");

            // If this is team chat, insert timestamp right after [TEAM]
            if (chatMessage.IsTeamChat && __result.StartsWith("[TEAM] "))
            {
                __result = "[TEAM] " + timestamp + __result.Substring(7);
            }
            else
            {
                __result = timestamp + __result;
            }
        }
    }
}