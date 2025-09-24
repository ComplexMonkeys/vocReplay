using MelonLoader;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using CardAnalogica;
using System.Linq;
using TMPro;
using System.Windows.Forms;

namespace vocReplay
{
    public static class BuildInfo
    {
        public const string Name = "vocReplay";
        public const string Description = "Mod that provides replay and text copying functions to the game";
        public const string Author = "ComplexMonkeys";
        public const string Version = "1.0.1";
        public const string DownloadLink = "https://github.com/ComplexMonkeys/vocReplay/releases";
    }

    // Stores information for playing audio
    public class AudioCardPlayed(int audioIdx = -1,
                           ushort checkId = 0,
                           float delay = -1.0f,
                           string? txtInfo = null,
                           string? txtInfoObj = null)
    {
        public int AudioIndex { get; set; } = audioIdx;
        public ushort CheckId { get; set; } = checkId;
        public float Delay { get; set; } = delay;
        public string TextInfo { get; set; } = txtInfo;
        public string TextInfoSceneObjectName { get; set; } = txtInfoObj;
    }

    public class ReplayableAudioMod : MelonMod
    {
        // ----
        // Helper methods that walk the hierarchy of a card and fetch text.
        // ----
        public static string TryGetTextOnCard(Transform root)
        {
            var t = root?.Find("CardGraphicObject")?
                       .Find("ImageCardObject")?
                       .Find("CardObject")?
                       .Find("BaseTrans")?
                       .Find("card_deform")?
                       .Find("CardInfo");

            var mesh = t?.gameObject.GetComponentInChildren<TextMeshPro>();
            return mesh?.text;
        }

        public static string CreateCopyPasteString(Transform root)
        {
            var baseT = root?.Find("BaseTrans")?.Find("card_deform");
            if (baseT == null) return "";

            var title = baseT.Find("CardTitle")?.GetComponent<TextMeshPro>();
            var info = baseT.Find("CardInfo")?.GetComponent<TextMeshPro>();

            var sb = new System.Text.StringBuilder();
            if (title != null) sb.AppendLine("【" + title.GetParsedText().Trim() + "】");
            if (info != null) sb.Append(info.GetParsedText().Trim());
            return sb.ToString();
        }

        public static string CreateCopyPasteStringForFrontBackCollections(Transform root)
        {
            var baseT = root?.Find("BaseTrans");
            if (baseT == null) return "";

            // Front side by default; back side when the card is rotated.
            string titleName = root.localRotation.y > 0 ? "CardTitleBack" : "CardTitleFront";
            string infoName = root.localRotation.y > 0 ? "CardInfoBack" : "CardInfoFront";

            var title = baseT.Find(titleName)?.GetComponent<TextMeshPro>();
            var info = baseT.Find(infoName)?.GetComponent<TextMeshPro>();

            var sb = new System.Text.StringBuilder();
            if (title != null) sb.AppendLine("【" + title.GetParsedText().Trim() + "】");
            if (info != null) sb.Append(info.GetParsedText().Trim());
            return sb.ToString();
        }

        public static string CreateCopyPasteStringForMiscellaneousCollections(Transform root)
            => CreateCopyPasteString(root);

        // Data containers.
        static readonly List<AudioCardPlayed> AudioCardsToUpdate = [];
        static readonly List<AudioCardPlayed> PlayableAudioCards = [];

        static int _lastVoiceIdPlayed = -1;
        static readonly ushort _lastVoiceCheckIdPlayed = 0;
        static float _lastVoiceDelayPlayed = -1.0f;

        // Force the voice manager to treat every voice as “not cached”.
        [HarmonyPatch(typeof(CardAnalogica.GameSoundManager), "VoicePlay")]
        static class VoicePlayPatch
        {
            static bool Prefix(ref ushort __1, int __0, float __2)
            {
                _lastVoiceIdPlayed = __0;
                _lastVoiceDelayPlayed = __2;
                __1 = 0;
                return true;
            }
        }

        /// Block close patch
        [HarmonyPatch(typeof(CardAnalogica.MainMenu), "Close")]
        static class MainMenu_ClosePatch
        {
            static bool Prefix(CardAnalogica.MainMenu __instance, bool __result, bool __0)
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    return false;
                }

                return true;
            }
        };

        // Block close patch the second
        [HarmonyPatch(typeof(CardAnalogica.MainMenu), "OnControlInput")]
        static class MainMenu_OnControlInputPatch
        {
            static bool Prefix(CardAnalogica.MainMenu __instance, UIFramework.ControlStatus.InputInfo __0)
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    return false;
                }

                return true;
            }
        };


        // Scan the event list and bind voices to cards.
        [HarmonyPatch(typeof(CardAnalogica.EventCmd3DMask), "DoCommand")]
        static class EventCmd3DMask_DoCommandPatch
        {
            static bool Prefix(CardAnalogica.EventCmd3DMask __instance)
            {
                if (__instance?.ParentExcutionInfo?.cmdList == null) return true;

                var cmdList = __instance.ParentExcutionInfo.cmdList;
                int maxIdx = cmdList.Count - 1;

                // Cards with text labels.
                for (int i = 0; i < maxIdx; ++i)
                {
                    var ev = cmdList[i];
                    if (ev.TryCast<EventCmdChoices>() != null)
                    {
                        var choices = ev.Cast<EventCmdChoices>();
                        var keyIds = new List<int>();
                        foreach (var c in choices.ChoicesList)
                        {
                            keyIds.Add(c.ChoicesID);
                        }

                        // Gather the formatted text for each choice.
                        var choiceTexts = new List<(int id, string txt)>();
                        foreach (int key in keyIds)
                        {
                            for (int j = i; j >= 0; --j)
                            {
                                var img = cmdList[j].TryCast<EventCmd3DImage>();
                                if (img != null && img.KeyID == key)
                                {
                                    string txt = ReplaceVariable.m_instance.ReplaceParamString(img.messageText);
                                    choiceTexts.Add((key, txt));
                                    break;
                                }
                            }
                        }

                        // Find the matching image animation and the voice that precedes it.
                        for (int j = i; j < maxIdx; ++j)
                        {
                            var anim = cmdList[j].TryCast<EventCmd3DImageAnim>();
                            if (anim == null) continue;

                            var match = choiceTexts.FirstOrDefault(ct => ct.id == anim.ID);
                            if (match.txt == null) continue;

                            // Walk backwards to locate the voice command.
                            for (int k = j; k >= 0; --k)
                            {
                                var snd = cmdList[k].TryCast<EventCmdSoundPlay>();
                                if (snd == null || snd.Type != 2) continue;

                                if (!HasAddedCard(snd.ID, snd.Delay, match.txt))
                                {
                                    AudioCardsToUpdate.Add(
                                        new AudioCardPlayed(snd.ID, 0, snd.Delay, match.txt, null));
                                }
                                break;
                            }
                            break; // one voice per choice
                        }
                    }
                }

                // Process voice commands
                var voiceIndices = new List<int>();
                for (int i = 0; i < maxIdx; ++i)
                {
                    var ev = cmdList[i];
                    if (ev.TryCast<EventCmdSoundPlay>()?.Type == 2)
                        voiceIndices.Add(i);
                }

                foreach (int idx in voiceIndices)
                {
                    var snd = cmdList[idx].Cast<EventCmdSoundPlay>();

                    // Find the nearest image‑animation key after the voice.
                    int animKey = -1;
                    for (int j = idx; j < maxIdx; ++j)
                    {
                        var anim = cmdList[j].TryCast<EventCmd3DImageAnim>();
                        if (anim != null) { animKey = anim.ID; break; }
                    }
                    if (animKey == -1) continue;

                    // Find the image that uses that key.
                    for (int j = idx; j >= 0; --j)
                    {
                        var img = cmdList[j].TryCast<EventCmd3DImage>();
                        if (img == null || img.KeyID != animKey) continue;

                        string txt = ReplaceVariable.m_instance.ReplaceParamString(img.messageText);
                        string imgKeyName = $"EventCmdImageKey{img.KeyID}";

                        if (!HasAddedCard(snd.ID, snd.Delay, txt))
                        {
                            AudioCardsToUpdate.Add(
                                new AudioCardPlayed(snd.ID, 0, snd.Delay, txt, imgKeyName));
                        }
                        break;
                    }
                }

                return true;
            }
        }

        // Utility checks for duplicates.
        public static bool HasAddedCard(int soundId, float delay, string txtInfo)
        {
            bool inPending = AudioCardsToUpdate.Any(a =>
                a.AudioIndex == soundId && a.Delay == delay && a.TextInfo == txtInfo);
            bool inReady = PlayableAudioCards.Any(a =>
                a.AudioIndex == soundId && a.Delay == delay && a.TextInfo == txtInfo);
            return inPending || inReady;
        }

        // Core update loop - remains much the same as original, just streamlined
        public override void OnUpdate()
        {
            bool scenarioActive = CardAnalogica.ScenarioTextManager.m_instance.nowViewIndex > 0;

            // F3 key – replay last voice (global) or current card.
            if (Input.GetKeyUp(KeyCode.F3))
            {
                // Global replay (when no scenario is active)
                if (!scenarioActive)
                {
                    if (_lastVoiceIdPlayed != -1)
                    {
                        CardAnalogica.GameSoundManager.m_instance
                            .VoicePlay(_lastVoiceIdPlayed, _lastVoiceCheckIdPlayed, _lastVoiceDelayPlayed);
                    }
                }

                // Card‑specific replay (scenario is active) 
                else
                {
                    var curIdx = CardAnalogica.ScenarioTextManager.m_instance.nowViewIndex;
                    var curInfo = CardAnalogica.ScenarioTextManager.m_instance
                                      .scenarioTextInfos[curIdx - 1];

                    // Grab the text that uniquely identifies the card.
                    string txt = TryGetTextOnCard(curInfo.CardObject.transform);
                    if (txt != null)
                    {
                        // Look for a *registered* audio entry that matches this text.
                        var card = PlayableAudioCards.FirstOrDefault(a => a.TextInfo == txt);

                        // Safety check
                        if (card != null && card.AudioIndex >= 0)
                        {
                            CardAnalogica.GameSoundManager.m_instance
                                .VoicePlay(card.AudioIndex, 0, card.Delay);
                        }
                    }
                }
            }

            // Register newly discovered cards (once per frame).
            if (scenarioActive)
            {
                foreach (var stInfo in CardAnalogica.ScenarioTextManager.m_instance.scenarioTextInfos)
                {
                    if (stInfo.CardObject == null) continue;

                    string txt = TryGetTextOnCard(stInfo.CardObject.transform);
                    if (txt == null) continue;

                    var pending = AudioCardsToUpdate.FirstOrDefault(a => a.TextInfo == txt);
                    if (pending != null)
                    {
                        PlayableAudioCards.Add(pending);
                        AudioCardsToUpdate.Remove(pending);
                    }
                }
            }

            // Ctrl + C – copy **scenario‑card** text (the big cards you read).
            var managers = GameObject.Find("Managers");
            if (managers != null)
            {
                var cam = managers.transform.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    var ray = cam.ScreenPointToRay(Input.mousePosition);
                    var hits = Physics.RaycastAll(ray.origin, ray.direction);
                    if (hits.Length > 0)
                    {
                        // skip CardMask if it is the first hit
                        var ordered = hits.OrderBy(h => h.distance).ToList();
                        var hit = ordered[0];
                        if (hit.collider?.gameObject?.name == "CardMask" && ordered.Count > 1)
                            hit = ordered[1];
                        if (hit.collider?.gameObject?.name == "CardObject")
                        {
                            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.C))
                            {
                                var txt = CreateCopyPasteString(hit.collider.transform);
                                if (!string.IsNullOrEmpty(txt))
                                    Clipboard.SetText(txt);
                            }
                        }
                    }
                }
            }

            // Ctrl + C – copy **collection** text (front/back cards, items, skills…)
            var mainCamObj = GameObject.Find("GUICamera");
            if (mainCamObj != null)
            {
                var cam = mainCamObj.GetComponent<Camera>();
                if (cam != null)
                {
                    var ray = cam.ScreenPointToRay(Input.mousePosition);
                    var hits = Physics.RaycastAll(ray.origin, ray.direction);
                    if (hits.Length > 0)
                    {
                        // Same skip-mask logic as before
                        var ordered = hits.OrderBy(h => h.distance).ToList();
                        var hit = ordered[0];
                        if (hit.collider?.gameObject?.name == "CardMask" && ordered.Count > 1)
                            hit = ordered[1];
                        // 

                        if (hit.collider?.gameObject?.name == "CardObject")
                        {
                            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.C))
                            {
                                var txt = CreateCopyPasteStringForFrontBackCollections(hit.collider.transform);
                                if (!string.IsNullOrEmpty(txt))
                                    Clipboard.SetText(txt);
                            }
                        }
                        else
                        {
                            // Miscellaneous items / skills etc.
                            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.C))
                            {
                                var txt = CreateCopyPasteStringForMiscellaneousCollections(hit.collider.transform);
                                if (!string.IsNullOrEmpty(txt))
                                    Clipboard.SetText(txt);
                            }
                        }
                    }
                }
            }
        }
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Utilities mod for the Voice of Cards games.");
        }
    }
}