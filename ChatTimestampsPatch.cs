using HarmonyLib;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChatTimestamps
{
    /// <summary>
    /// Multi-flavor chat timestamp patches for Puck.
    ///
    /// Three chat delivery / rendering flavors:
    ///
    /// 1) Standard ChatMessage (IsSystem=false, vanilla rendering):
    ///    UIChat.AddChatMessage Postfix walks the last chat row and
    ///    prepends "[HH:mm:ss] " to the label text. Works because
    ///    standard rendering produces a plain Label.
    ///
    /// 2) System-message rich text (IsSystem=true, PHL/CompTweaks/TagMod):
    ///    ChatManager.AddChatMessage Prefix detects IsSystem=true and
    ///    splices timestamp into Content. Two sub-flavors:
    ///    (a) &lt;noparse&gt;username&lt;/noparse&gt;: msg — splice inside
    ///        the noparse block.
    ///    (b) Generic (TagMod [[G|...]] / [[N|...]] markers) — find a
    ///        known player's username before ": " and splice timestamp
    ///        into Content in front of it.
    ///    The UIChat.AddChatMessage Postfix then handles the visual
    ///    injection the same way.
    ///
    /// 3) TagMod LiveGradient Neon [[N|...]] override:
    ///    LiveGradient's Postfix at Priority.Last - 1 replaces the chat
    ///    label with a flex-row [tagLabel, restLabel], discarding any
    ///    text-level timestamp. Our Postfix at Priority.Last - 100
    ///    detects the flex container and prepends timestamp to the rest
    ///    label's text.
    ///
    /// ThreadStatic _pendingTimestamp bridges Prefix -> Postfix for
    /// flavors 2 and 3. Flavor 1 generates the timestamp directly in
    /// the Postfix.
    /// </summary>
    [HarmonyPatch]
    public static class TimestampChatPatches
    {
        // Matches "[HH:mm:ss] " at the start — the shape we produce.
        private static readonly Regex _alreadyTimestamped =
            new Regex(@"^\[\d{2}:\d{2}:\d{2}\] ", RegexOptions.Compiled);

        // Captures text inside the first <noparse>...</noparse> block.
        private static readonly Regex _noparseBlock =
            new Regex(@"<noparse>(?<u>.*?)</noparse>", RegexOptions.Compiled);

        // Carries timestamp from ChatManager Prefix to UIChat Postfix.
        [ThreadStatic]
        private static string _pendingTimestamp;

        // ------------------------------------------------------------------
        // Flavor 2 — Prefix on ChatManager.AddChatMessage (system messages)
        // ------------------------------------------------------------------

        [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.AddChatMessage))]
        [HarmonyPriority(Priority.First)]
        internal static class ChatManager_AddChatMessage_Prefix
        {
            private static void Prefix(ChatMessage chatMessage)
            {
                _pendingTimestamp = null;
                try
                {
                    if (chatMessage == null) return;
                    if (!chatMessage.IsSystem) return;

                    string ts = DateTime.Now.ToString("[HH:mm:ss] ");
                    TryHandleSystemMessage(chatMessage, ts);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ChatTimestamps] cm prefix failed: " + e.Message);
                }
            }
        }

        // ------------------------------------------------------------------
        // Flavors 1 & 3 — Postfix on UIChat.AddChatMessage
        // ------------------------------------------------------------------

        [HarmonyPatch(typeof(UIChat), "AddChatMessage")]
        [HarmonyPriority(Priority.Last - 100)]
        internal static class UIChat_AddChatMessage_Postfix
        {
            private static void Postfix(UIChat __instance)
            {
                if (__instance == null) return;

                try
                {
                    // Find the messages container.
                    var root = AccessTools.Field(typeof(UIChat), "messages")
                        ?.GetValue(__instance) as VisualElement;
                    if (root == null)
                    {
                        var sv = AccessTools.Field(typeof(UIChat), "scrollView")
                            ?.GetValue(__instance) as ScrollView;
                        root = sv?.contentContainer;
                    }
                    if (root == null || root.childCount == 0) return;

                    var lastRow = root[root.childCount - 1];
                    if (lastRow == null) return;

                    // Determine the timestamp to inject.
                    // Flavor 2 set it in the Prefix; Flavor 1 generates it now.
                    string ts = _pendingTimestamp;
                    _pendingTimestamp = null;
                    if (ts == null)
                    {
                        // Flavor 1 (standard message) — generate timestamp now.
                        // Check if the label already has a timestamp (idempotency).
                        ts = DateTime.Now.ToString("[HH:mm:ss] ");
                    }

                    // Case A: LiveGradient neon flex container [tagLabel, restLabel]
                    var flex = FindNeonFlexContainer(lastRow);
                    if (flex != null && flex.childCount >= 2
                        && flex[flex.childCount - 1] is Label restLabel)
                    {
                        string cur = restLabel.text ?? "";
                        if (!cur.StartsWith(ts, StringComparison.Ordinal)
                            && !_alreadyTimestamped.IsMatch(cur.TrimStart()))
                        {
                            int i = 0;
                            while (i < cur.Length && cur[i] == ' ') i++;
                            restLabel.text = cur.Substring(0, i) + ts + cur.Substring(i);
                        }
                        return;
                    }

                    // Case B: plain Label — prepend timestamp to text.
                    Label plainLabel = lastRow as Label ?? lastRow.Q<Label>();
                    if (plainLabel != null)
                    {
                        string cur = plainLabel.text ?? "";
                        if (!cur.StartsWith(ts, StringComparison.Ordinal)
                            && !_alreadyTimestamped.IsMatch(cur))
                        {
                            plainLabel.text = ts + cur;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ChatTimestamps] uichat postfix failed: " + e.Message);
                }
            }
        }

        // ------------------------------------------------------------------
        // System-message internals (Flavor 2)
        //
        // For system messages we cannot reliably mutate ChatMessage.Content
        // from a Prefix because (a) ChatMessage is a struct (passed by
        // value) and (b) other mods like TRL may also be mutating Content
        // at the same priority. Instead we only set _pendingTimestamp and
        // let the UIChat.AddChatMessage Postfix inject the timestamp
        // visually into the rendered label — which runs after all other
        // mods have finished their Prefix/Postfix work.
        // ------------------------------------------------------------------

        private static void TryHandleSystemMessage(ChatMessage chatMessage, string ts)
        {
            string content = chatMessage.Content.ToString();
            if (string.IsNullOrEmpty(content)) return;

            // Must look like a chat line (has ": ").
            int colonIdx = content.IndexOf(": ", StringComparison.Ordinal);
            if (colonIdx < 0) return;

            // Guard: if Content already contains a timestamp, skip.
            if (_alreadyTimestamped.IsMatch(content)) return;

            // Set the pending timestamp so the Postfix injects it visually.
            _pendingTimestamp = ts;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Finds the first flex-row container with >= 2 children — the
        /// [tagLabel, restLabel] shape from LiveGradient's Neon handler.
        /// Returns null for plain-Label rows.
        /// </summary>
        private static VisualElement FindNeonFlexContainer(VisualElement el)
        {
            if (el == null) return null;
            if (!(el is Label) && el.childCount >= 2
                && el.style.flexDirection.value == FlexDirection.Row)
                return el;
            for (int i = 0; i < el.childCount; i++)
            {
                var f = FindNeonFlexContainer(el[i]);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>
        /// Finds the best-matching Player for a string containing their
        /// username. Prefers longest match to avoid substring false positives.
        /// </summary>
        private static Player FindPlayerByUsername(PlayerManager pm, string haystack)
        {
            var all = pm.GetPlayers();
            if (all == null) return null;
            Player best = null;
            int bestLen = 0;
            foreach (var p in all)
            {
                if (p == null) continue;
                string pn = p.Username.Value.ToString();
                if (string.IsNullOrEmpty(pn)) continue;
                if (haystack.IndexOf(pn, StringComparison.Ordinal) >= 0 && pn.Length > bestLen)
                {
                    best = p; bestLen = pn.Length;
                }
                else if (pn.IndexOf(haystack, StringComparison.Ordinal) >= 0 && pn.Length > bestLen)
                {
                    best = p; bestLen = pn.Length;
                }
            }
            return best;
        }
    }
}
