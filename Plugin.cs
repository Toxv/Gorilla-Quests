using System;
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using GorillaNetworking;
using GorillaGameModes;
using GorillaTag;
using GorillaTagScripts;
using BepInEx;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using System.Text.RegularExpressions;
using ExitGames.Client.Photon;
using System.Data.SqlTypes;
using System.IO;
using System.Reflection;
using HarmonyLib;
using CSCore;
using Photon.Realtime;
using Gorilla_Quests.Patches;
using BuildSafe;
using System.Threading;
using Unity.Mathematics;
using static System.Net.WebRequestMethods;
using System.Text;

namespace Gorilla_Quests
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        private bool inRoom;
        public static GameObject motd = null;
        public static GameObject motdText = null;
        private List<Quest> advancedQuests = new List<Quest>();
        private List<Quest> expertQuests = new List<Quest>();
        private List<Quest> quests = new List<Quest>();
        private List<Quest> selectedQuests = new List<Quest>();
        private int totalXP = 0;
        private bool Debounce = false;
        public static bool JoinedRoom = false;
        private GorillaTagManager gtmanager;

        private void SaveAllQuestProgress()
        {
            PlayerPrefs.SetInt("QuestCount", selectedQuests.Count);

            for (int i = 0; i < selectedQuests.Count; i++)
            {
                Quest quest = selectedQuests[i];
                PlayerPrefs.SetString($"Quest_{i}_Name", quest.Name);
                PlayerPrefs.SetInt($"Quest_{i}_Progress", quest.Progress);
                PlayerPrefs.SetInt($"Quest_{i}_Goal", quest.Goal);
                PlayerPrefs.SetInt($"Quest_{i}_XP", quest.XP);
                PlayerPrefs.SetInt($"Quest_{i}_Count", quest.Count ? 1 : 0);
                PlayerPrefs.SetInt($"Quest_{i}_RoomTrigger", quest.RoomTrigger ? 1 : 0);
                PlayerPrefs.SetInt($"Quest_{i}_Gamemode", quest.Gamemode ? 1 : 0);
                PlayerPrefs.SetString($"Quest_{i}_GamemodeName", quest.GamemodeName ?? string.Empty);
                PlayerPrefs.SetInt($"Quest_{i}_Map", quest.Map ? 1 : 0);
                PlayerPrefs.SetString($"Quest_{i}_Mapname", quest.Mapname ?? string.Empty);
                PlayerPrefs.SetInt($"Quest_{i}_Queue", quest.Queue ? 1 : 0);
                PlayerPrefs.SetString($"Quest_{i}_QueueName", quest.QueueName ?? string.Empty);
                PlayerPrefs.SetString($"Quest_{i}_QuestType", quest.Type.ToString());
            }
            PlayerPrefs.SetInt("TotalXP", totalXP);
            PlayerPrefs.SetInt("Streal", Streak);
            PlayerPrefs.Save();
        }

        private void LoadQuestProgress()
        {
            selectedQuests.Clear();
            int questCount = PlayerPrefs.GetInt("QuestCount", 0);

            Debug.Log($"Loading quests: {questCount} found in PlayerPrefs.");

            for (int i = 0; i < questCount; i++)
            {
                string questName = PlayerPrefs.GetString($"Quest_{i}_Name", null);
                if (!string.IsNullOrEmpty(questName))
                {
                    int progress = PlayerPrefs.GetInt($"Quest_{i}_Progress", 0);
                    int goal = PlayerPrefs.GetInt($"Quest_{i}_Goal", 0);
                    int xp = PlayerPrefs.GetInt($"Quest_{i}_XP", 0);
                    bool count = PlayerPrefs.GetInt($"Quest_{i}_Count", 0) == 1;
                    bool roomTrigger = PlayerPrefs.GetInt($"Quest_{i}_RoomTrigger", 0) == 1;
                    bool gamemode = PlayerPrefs.GetInt($"Quest_{i}_Gamemode", 0) == 1;
                    string gamemodeName = PlayerPrefs.GetString($"Quest_{i}_GamemodeName", string.Empty);
                    bool map = PlayerPrefs.GetInt($"Quest_{i}_Map", 0) == 1;
                    string mapname = PlayerPrefs.GetString($"Quest_{i}_Mapname", string.Empty);
                    bool queue = PlayerPrefs.GetInt($"Quest_{i}_Queue", 0) == 1;
                    string queueName = PlayerPrefs.GetString($"Quest_{i}_QueueName", string.Empty);

                    string questTypeString = PlayerPrefs.GetString($"Quest_{i}_QuestType", QuestTypeCooldown.Normal.ToString());
                    if (!Enum.TryParse(questTypeString, out QuestTypeCooldown questType))
                    {
                        questType = QuestTypeCooldown.Normal;
                        Debug.LogWarning($"Failed to parse QuestType for {questName}. Defaulting to Normal.");
                    }

                    Quest loadedQuest = new Quest(questName, goal, xp, count, roomTrigger, gamemode, gamemodeName, map, mapname, queue, queueName, questType)
                    {
                        Progress = progress
                    };


                    var matchingQuest = quests.FirstOrDefault(q => q.Name == loadedQuest.Name) ??
                                        advancedQuests.FirstOrDefault(q => q.Name == loadedQuest.Name) ??
                                        expertQuests.FirstOrDefault(q => q.Name == loadedQuest.Name);

                    if (matchingQuest != null)
                    {
                        matchingQuest.Progress = loadedQuest.Progress;
                        selectedQuests.Add(matchingQuest);
                    }
                    else
                    {
                        Debug.LogWarning($"Quest not found in any list: {loadedQuest.Name}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Quest name at index {i} is null or empty.");
                }
            }

            totalXP = PlayerPrefs.GetInt("TotalXP", 0);
            Streak = PlayerPrefs.GetInt("Streak", 0);
            if (selectedQuests.Count == 0)
            {
                Debug.LogWarning("No quest data found. Selecting random quests.");
                SelectRandomQuests();
            }
        }


        void Start()
        {
            HarmonyPatches.ApplyHarmonyPatches();
              if (PlayerPrefs.GetInt("DiscordPopupDismissed", 0) == 1)
             {
                showPopup = false;
              }


              float windowWidth = 300;
               float windowHeight = 150;
               windowRect = new Rect((Screen.width - windowWidth) / 2, (Screen.height - windowHeight) / 2, windowWidth, windowHeight);
            GorillaTagger.OnPlayerSpawned(playerSpawned);
        }
        void EventReceived(EventData photonEvent)
        {
            /* bool flag = photonEvent.Code == 2 && PhotonNetwork.CurrentRoom.IsVisible && !PhotonNetwork.CurrentRoom.CustomProperties["gameMode"].ToString().Contains("MODDED_");
             if (flag)
             {
                 object[] data = (object[])photonEvent.CustomData;
                 NetPlayer netPlayer = GameMode.ParticipatingPlayers.FirstOrDefault((NetPlayer player) => player.UserId == (string)data[0]);
                 int num = PhotonNetwork.CurrentRoom.Players.Count - this.gtmanager.currentInfected.Count;
                 bool flag2 = netPlayer != null && netPlayer.IsLocal;
                 if (flag2)
                 {
                     int num2 = 5 * num;
                     this.money += (float)num2;
                     PlayerPrefs.SetFloat("gamblemoney", this.money);
                     Debug.Log(string.Format("Gave user ${0}", num2));
                 }
             }
             if (photonEvent.Code == 2 || photonEvent.Code == GorillaTagManager.ReportInfectionTagEvent && PhotonNetwork.CurrentRoom.IsVisible && !PhotonNetwork.CurrentRoom.CustomProperties["gameMode"].ToString().Contains("MODDED_"))
             {
                 object[] data = (object[])photonEvent.CustomData;
                 NetPlayer taggingPlayer = GameMode.ParticipatingPlayers.FirstOrDefault((NetPlayer player) => player.UserId == (string)data[0]);
                 int unTaggedPlayers = PhotonNetwork.CurrentRoom.Players.Count - this.gtmanager.currentInfected.Count;
                 if (taggingPlayer != null && taggingPlayer.IsLocal)
                 {
                     Debug.Log("Tagged Someone");
                 }
                 print("tagged someone");
             }
             if (photonEvent.Code == GorillaTagManager.ReportInfectionTagEvent)
             {
                 object[] data = (object[])photonEvent.CustomData;
                 NetPlayer taggingPlayer = GameMode.ParticipatingPlayers.FirstOrDefault(player => player.UserId == (string)data[0]);
                 int unTaggedPlayers = PhotonNetwork.CurrentRoom.Players.Count - gtmanager.currentInfected.Count;
                 if (taggingPlayer != null && taggingPlayer.IsLocal)
                 {
                     print("taggedSomeone");
                 }
                 print("taggedSomeone");
             }*/
        }


        void OnEnable()
        {
            HarmonyPatches.ApplyHarmonyPatches();
        }

        void OnDisable()
        {
            HarmonyPatches.RemoveHarmonyPatches();
        }
        private bool PlayerHasQuest(string questName)
        {
            return selectedQuestss.Any(q => q.Name == questName);
        }
        private List<Quest> selectedQuestss = new List<Quest>();
        private HashSet<Quest> activeQuests = new HashSet<Quest>();

        private IEnumerator CountUpQuestProgress(Quest quest)
        {
            int trueCount = boolProperties.Count(prop => (bool)prop.GetValue(quest));
            if (trueCount == 1 && quest.Count && !quest.IsOnCooldown)
            {
                if (activeQuests.Contains(quest))
                {
                    yield break;
                }
                activeQuests.Add(quest);
                while (quest.Progress < quest.Goal)
                {
                    yield return new WaitForSeconds(60f);
                    IncrementProgress(1, quest);
                    CheckAndStartCountQuest();
                    StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                    UpdateWatchText();
                }
                CompleteQuest(quest, DetermineQuestType(quest));
                StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                UpdateWatchText();
                activeQuests.Remove(quest);
                SaveAllQuestProgress();
            }
        }
        private bool IsQuestActive(Quest quest)
        {
            return activeQuests.Contains(quest);
        }

        private string DetermineQuestTypeTiming(Quest quest)
        {

            if (quests.Contains(quest))
                return "Regular";
            if (advancedQuests.Contains(quest))
                return "Advanced";
            if (expertQuests.Contains(quest))
                return "Expert";

            return "Unknown";
        }
        private bool PlayerHasQuestWithRoomTrigger(Quest quest)
        {
            return selectedQuests.Any(quest => quest.RoomTrigger);
        }

        private bool PlayerHasQuestWithGamemode(Quest quest)
        {
            return selectedQuests.Any(quest => quest.Gamemode);
        }

        private bool PlayerHasQuestWithCount(Quest quest)
        {
            return selectedQuests.Any(quest => quest.Count);
        }

        private (bool hasQuest, string mapName) PlayerHasQuestWithMap(Quest quest)
        {
            var questWithMap = selectedQuests.FirstOrDefault(quest => quest.Map);
            return (questWithMap != null, questWithMap?.Mapname);
        }
        private Dictionary<string, string> taskMappings = new Dictionary<string, string>
        {
            { "Complete 1 Round Of Infection", "1 Rnd Infec" },
            { "Join a Lobby", "Join Lobby" },
            { "Play for 10 Minutes", "Play 10 M" },
            { "Play Casual Mode", "Play Casual" },
            { "Play Infection Mode", "Play Infec" },
            { "Play Hunt Mode", "Play Hunt" },
            { "Play Forest for 5 Minutes", "For 5 M" },
            { "Play Forest in Casual Mode", "For Cas." },
            { "Play Forest in Infection Mode", "For Inf." },
            { "Play Forest in Hunt Mode", "For Hunt" },
            { "Play Canyons for 5 Minutes", "Can 5 M" },
            { "Play Canyons in Casual Mode", "Can Cas." },
            { "Play Canyons in Infection Mode", "Can Inf." },
            { "Play Canyons in Hunt Mode", "Can Hunt" },
            { "Play Mountain for 5 Minutes", "Mtn 5 M" },
            { "Play Mountain in Casual Mode", "Mtn Casual" },
            { "Play Mountain in Infection Mode", "Mtn Infec" },
            { "Play Mountain in Hunt Mode", "Mtn Hunt" },
            { "Play City for 5 Minutes", "City 5 M" },
            { "Play City in Casual Mode", "City Cas." },
            { "Play City in Infection Mode", "City Inf." },
            { "Play City in Hunt Mode", "City Hunt" },
            { "Play Beach for 5 Minutes", "Beach 5 M" },
            { "Play Beach in Casual Mode", "Beach Cas." },
            { "Play Beach in Infection Mode", "Beach Inf." },
            { "Play Beach in Hunt Mode", "Beach Hunt" },
            { "Play Rotation for 5 Minutes", "Rot 5 M" },
            { "Play Rotation in Casual Mode", "Rot Casual" },
            { "Play Rotation in Infection Mode", "Rot Infec" },
            { "Play Rotation in Hunt Mode", "Rot Hunt" },
            { "Play Basement for 5 Minutes", "Base 5 M" },
            { "Play Basement in Casual Mode", "Base Cas." },
            { "Play Basement in Infection Mode", "Base Inf." },
            { "Play Basement in Hunt Mode", "Base Hunt" },
            { "Play Metro for 5 Minutes", "Metro 5 M" },
            { "Play Metro in Casual Mode", "Metro Cas." },
            { "Play Metro in Infection Mode", "Metro Inf." },
            { "Play Metro in Hunt Mode", "Metro Hunt" },
            { "Play for 20 Minutes", "Play 20 M" },
            { "Complete 2 Rounds of Infection", "2 Rnd Infec" },
            { "Play Forest for 10 Minutes", "For 10 M" },
            { "Play Forest in Casual Mode for 10 Minutes", "For Cas 10 M" },
            { "Play Forest in Infection Mode for 10 Minutes", "For Inf 10 M" },
            { "Play Forest in Hunt Mode for 10 Minutes", "For Hnt 10 M" },
            { "Play Canyons for 10 Minutes", "Can 10 M" },
            { "Play Canyons in Casual Mode for 10 Minutes", "Can Cas 10 M" },
            { "Play Canyons in Infection Mode for 10 Minutes", "Can Inf 10 M" },
            { "Play Canyons in Hunt Mode for 10 Minutes", "Can Hnt 10 M" },
            { "Play Mountain for 10 Minutes", "Mtn 10 M" },
            { "Play Mountain in Casual Mode for 10 Minutes", "Mtn Cas 10 M" },
            { "Play Mountain in Infection Mode for 10 Minutes", "Mtn Inf 10 M" },
            { "Play Mountain in Hunt Mode for 10 Minutes", "Mtn Hnt 10 M" },
            { "Play City for 10 Minutes", "City 10 M" },
            { "Play City in Casual Mode for 10 Minutes", "City Cas 10 M" },
            { "Play City in Infection Mode for 10 Minutes", "City Inf 10 M" },
            { "Play City in Hunt Mode for 10 Minutes", "City Hnt 10 M" },
            { "Play Beach for 10 Minutes", "Beach 10 M" },
            { "Play Beach in Casual Mode for 10 Minutes", "Beach Cas 10 M" },
            { "Play Beach in Infection Mode for 10 Minutes", "Beach Inf 10 M" },
            { "Play Beach in Hunt Mode for 10 Minutes", "Beach Hnt 10 M" },
            { "Play Rotation for 10 Minutes", "Rot 10 M" },
            { "Play Rotation in Casual Mode for 10 Minutes", "Rot Cas 10 M" },
            { "Play Rotation in Infection Mode for 10 Minutes", "Rot Inf 10 M" },
            { "Play Rotation in Hunt Mode for 10 Minutes", "Rot Hnt 10 M" },
            { "Play Basement for 10 Minutes", "Base 10 M" },
            { "Play Basement in Casual Mode for 10 Minutes", "Base Cas 10 M" },
            { "Play Basement in Infection Mode for 10 Minutes", "Base Inf 10 M" },
            { "Play Basement in Hunt Mode for 10 Minutes", "Base Hnt 10 M" },
            { "Play Metro for 10 Minutes", "Metro 10 M" },
            { "Play Metro in Casual Mode for 10 Minutes", "Metro Cas 10 M" },
            { "Play Metro in Infection Mode for 10 Minutes", "Metro Inf 10 M" },
            { "Play Metro in Hunt Mode for 10 Minutes", "Metro Hnt 10 M" },
            { "Complete 5 Rounds Of Infection", "5 Rnd Infec" },
            { "Play for 30 Minutes", "Play 30 M" },
            { "Play Forest for 15 Minutes", "For 15 M" },
            { "Play Forest in Casual Mode for 15 Minutes", "For Cas 15 M" },
            { "Play Forest in Infection Mode for 15 Minutes", "For Inf 15 M" },
            { "Play Forest in Hunt Mode for 15 Minutes", "For Hnt 15 M" },
            { "Play Canyons for 15 Minutes", "Can 15 M" },
            { "Play Canyons in Casual Mode for 15 Minutes", "Can Cas 15 M" },
            { "Play Canyons in Infection Mode for 15 Minutes", "Can Inf 15 M" },
            { "Play Canyons in Hunt Mode for 15 Minutes", "Can Hnt 15 M" },
            { "Play Mountain for 15 Minutes", "Mtn 15 M" },
            { "Play Mountain in Casual Mode for 15 Minutes", "Mtn Cas 15 M" },
            { "Play Mountain in Infection Mode for 15 Minutes", "Mtn Inf 15 M" },
            { "Play Mountain in Hunt Mode for 15 Minutes", "Mtn Hnt 15 M" },
            { "Play City for 15 Minutes", "City 15 M" },
            { "Play City in Casual Mode for 15 Minutes", "City Cas 15 M" },
            { "Play City in Infection Mode for 15 Minutes", "City Inf 15 M" },
            { "Play City in Hunt Mode for 15 Minutes", "City Hnt 15 M" },
            { "Play Beach for 15 Minutes", "Beach 15 M" },
            { "Play Beach in Casual Mode for 15 Minutes", "Beach Cas 15 M" },
            { "Play Beach in Infection Mode for 15 Minutes", "Beach Inf 15 M" },
            { "Play Beach in Hunt Mode for 15 Minutes", "Beach Hnt 15 M" },
            { "Play Rotation for 15 Minutes", "Rot 15 M" },
            { "Play Rotation in Casual Mode for 15 Minutes", "Rot Cas 15 M" },
            { "Play Rotation in Infection Mode for 15 Minutes", "Rot Inf 15 M" },
            { "Play Rotation in Hunt Mode for 15 Minutes", "Rot Hnt 15 M" },
            { "Play Basement for 15 Minutes", "Base 15 M" },
            { "Play Basement in Casual Mode for 15 Minutes", "Base Cas 15 M" },
            { "Play Basement in Infection Mode for 15 Minutes", "Base Inf 15 M" },
            { "Play Basement in Hunt Mode for 15 Minutes", "Base Hnt 15 M" },
            { "Play Metro for 15 Minutes", "Metro 15 M" },
            { "Play Metro in Casual Mode for 15 Minutes", "Metro Cas 15 M" },
            { "Play Metro in Infection Mode for 15 Minutes", "Metro Inf 15 M" },
            { "Play Metro in Hunt Mode for 15 Minutes", "Metro Hnt 15 M" },
            { "Play for 60 Minutes", "Play 60 M" },
            { "Complete 10 Rounds Of Infection", "10 Rnd Infec" },
            { "Play Forest for 30 Minutes", "For 30 M" },
            { "Play Forest in Casual Mode for 30 Minutes", "For Cas 30 M" },
            { "Play Forest in Infection Mode for 30 Minutes", "For Inf 30 M" },
            { "Play Forest in Hunt Mode for 30 Minutes", "For Hnt 30 M" },
            { "Play Canyons for 30 Minutes", "Can 30 M" },
            { "Play Canyons in Casual Mode for 30 Minutes", "Can Cas 30 M" },
            { "Play Canyons in Infection Mode for 30 Minutes", "Can Inf 30 M" },
            { "Play Canyons in Hunt Mode for 30 Minutes", "Can Hnt 30 M" },
            { "Play Mountain for 30 Minutes", "Mtn 30 M" },
            { "Play Mountain in Casual Mode for 30 Minutes", "Mtn Cas 30 M" },
            { "Play Mountain in Infection Mode for 30 Minutes", "Mtn Inf 30 M" },
            { "Play Mountain in Hunt Mode for 30 Minutes", "Mtn Hnt 30 M" },
            { "Play City for 30 Minutes", "City 30 M" },
            { "Play City in Casual Mode for 30 Minutes", "City Cas 30 M" },
            { "Play City in Infection Mode for 30 Minutes", "City Inf 30 M" },
            { "Play City in Hunt Mode for 30 Minutes", "City Hnt 30 M" },
            { "Play Beach for 30 Minutes", "Beach 30 M" },
            { "Play Beach in Casual Mode for 30 Minutes", "Beach Cas 30 M" },
            { "Play Beach in Infection Mode for 30 Minutes", "Beach Inf 30 M" },
            { "Play Beach in Hunt Mode for 30 Minutes", "Beach Hnt 30 M" },
            { "Play Rotation for 30 Minutes", "Rot 30 M" },
            { "Play Rotation in Casual Mode for 30 Minutes", "Rot Cas 30 M" },
            { "Play Rotation in Infection Mode for 30 Minutes", "Rot Inf 30 M" },
            { "Play Rotation in Hunt Mode for 30 Minutes", "Rot Hnt 30 M" },
            { "Play Basement for 30 Minutes", "Base 30 M" },
            { "Play Basement in Casual Mode for 30 Minutes", "Base Cas 30 M" },
            { "Play Basement in Infection Mode for 30 Minutes", "Base Inf 30 M" },
            { "Play Basement in Hunt Mode for 30 Minutes", "Base Hnt 30 M" },
            { "Play Metro for 30 Minutes", "Metro 30 M" },
            { "Play Metro in Casual Mode for 30 Minutes", "Metro Cas 30 M" },
            { "Play Metro in Infection Mode for 30 Minutes", "Metro Inf 30 M" },
            { "Play Metro in Hunt Mode for 30 Minutes", "Metro Hnt 30 M" },
        };
        string FormatMap(string map)// tehee wryser not stolen code
        {
            string formattedmap;
            switch (map)
            {
                case "CANYON":
                    formattedmap = "CANYONS";
                    break;
                case "CITYWITHSKYJUNGLE":
                    formattedmap = "CITY";
                    break;
                case "CAVE":
                    formattedmap = "CAVES";
                    break;
                case "ROTATING":
                    formattedmap = "ROTATING";
                    break;
                default:
                    formattedmap = map;
                    break;
            }
            return formattedmap;
        }
        private string GetCurrentGamemode()
        {
            var currentgamemodestring = GorillaComputer.instance.currentGameMode.Value;
            return currentgamemodestring;
        }
        private string GetCurrentQueue()
        {
            var currentqueuestring = GorillaComputer.instance.currentQueue;
            return currentqueuestring;
        }
        private float timeSpentInMap = 0f;
        private float timeCheckInterval = 60f;
        private Coroutine timeTrackingCoroutine;
        private PropertyInfo[] boolProperties = typeof(Quest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(prop => prop.PropertyType == typeof(bool))
            .ToArray();
        private IEnumerator TrackTimeInMap(Quest quest)
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                if (quest.Map)
                {
                    string mapName = quest.Mapname.ToUpper();
                    string currentMap = FormatMap(MapPatch.ActiveZones.First().ToString().ToUpper());
                    if (mapName.Equals(currentMap, StringComparison.OrdinalIgnoreCase))
                    {
                        timeSpentInMap += 1f;
                        Debug.Log($"Time spent in quest map '{mapName}': {timeSpentInMap} seconds");
                        if (timeSpentInMap >= timeCheckInterval)
                        {
                            IncrementProgress(1, quest);
                            if (PhotonNetwork.CurrentRoom.PlayerCount < 3)
                            {
                                CompleteQuest(quest, DetermineQuestType(quest));
                                Debug.Log($"Quest completed due to player count < 3: {quest.Name}");
                            }
                            timeSpentInMap = 0f;
                        }
                    }
                    else
                    {
                        timeSpentInMap = 0f;
                        Debug.Log($"Exited quest map. Time tracking reset for '{mapName}'.");
                    }
                }
                else
                {
                    Debug.LogError("Quest does not require a specific map.");
                }
            }
        }
        private void OnRoomJoin(string roomName)
        {
            // LastInRound();
            Debug.Log($"Joined a Room: {roomName}");

            string currentGamemode2 = GetCurrentGamemode();
            string currentQueue2 = GetCurrentQueue();

            Debug.Log($"Current Gamemode: {currentGamemode2}");
            Debug.Log($"Current Queue: {currentQueue2}");
            string currentMap = FormatMap(MapPatch.ActiveZones.First().ToString().ToUpper());
            Debug.Log($"Current Map: {currentMap}");
            var questsWithRoomTrigger = selectedQuests.Where(quest => quest.RoomTrigger).ToList();

            if (questsWithRoomTrigger.Any())
            {
                foreach (var quest in questsWithRoomTrigger)
                {

                    int trueCount = boolProperties.Count(prop => (bool)prop.GetValue(quest));

                    if (trueCount == 1 && quest.RoomTrigger)
                    {
                        print("true");
                        Debug.Log(quest);
                        IncrementProgress(1, quest);
                        StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                        UpdateWatchText();
                    }
                    if (quest.Map)
                    {
                        var mapName = quest.Mapname.ToUpper(); ;
                        if (!string.IsNullOrEmpty(mapName))
                        {
                            string map = FormatMap(MapPatch.ActiveZones.First().ToString().ToUpper());
                            if (mapName.ToUpper() == map)
                            {
                                IncrementProgress(1, quest);
                                StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                                UpdateWatchText();
                                print($"In Map {mapName} or {map}");
                            }
                            Debug.Log($"This quest requires the map: {mapName}");
                        }
                        else
                        {
                            Debug.LogError("This quest requires a map but none is specified.");
                        }
                    }
                    if (quest.Count)
                    {
                        int progressIncrement = 0;
                        if (quest.Map && quest.Gamemode)
                        {
                            string requiredGamemode = quest.GamemodeName.ToUpper();
                            string currentGamemode = GetCurrentGamemode();

                            string mapName = quest.Mapname.ToUpper();
                            string currentMap2 = FormatMap(MapPatch.ActiveZones.First().ToString().ToUpper());

                            if (mapName.Equals(currentMap2, StringComparison.OrdinalIgnoreCase) &&
                                requiredGamemode.Equals(currentGamemode, StringComparison.OrdinalIgnoreCase))
                            {
                                if (timeTrackingCoroutine == null)
                                {
                                    timeTrackingCoroutine = StartCoroutine(TrackTimeInMap(quest));
                                }
                                Debug.Log($"Both map and gamemode requirements met for quest: {quest.Name}");
                            }
                            else
                            {
                                Debug.Log($"Quest requirements not met. Required Map: {mapName}, Required Gamemode: {requiredGamemode}. Current Map: {currentMap}, Current Gamemode: {currentGamemode}.");
                            }
                        }
                        else if (quest.Map)
                        {
                            string mapName = quest.Mapname.ToUpper();
                            string currentMap3 = FormatMap(MapPatch.ActiveZones.First().ToString().ToUpper());

                            if (mapName.Equals(currentMap3, StringComparison.OrdinalIgnoreCase))
                            {
                                if (timeTrackingCoroutine == null)
                                {
                                    timeTrackingCoroutine = StartCoroutine(TrackTimeInMap(quest));
                                }
                                Debug.Log($"Map requirement met for quest: {quest.Name}");
                            }
                            else
                            {
                                Debug.Log($"This quest requires a different map: {mapName}. Current map: {currentMap}.");
                            }
                        }
                    }
                    if (quest.Gamemode)
                    {
                        string requiredGamemode = quest.GamemodeName.ToUpper(); ;
                        if (!string.IsNullOrEmpty(requiredGamemode))
                        {
                            string currentGamemode = GetCurrentGamemode();
                            if (requiredGamemode.Equals(currentGamemode, StringComparison.OrdinalIgnoreCase))
                            {
                                IncrementProgress(1, quest);
                                StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                                UpdateWatchText();
                            }
                            else
                            {
                                Debug.Log($"This quest requires a different gamemode: {requiredGamemode}. Current gamemode: {currentGamemode}.");
                            }
                        }
                        else
                        {
                            Debug.LogError("This quest requires a gamemode but none is specified.");
                        }
                    }
                    else
                    {
                        Debug.Log("This quest does not require a specific gamemode.");
                    }
                    if (quest.Queue)
                    {
                        string currentQueue = GetCurrentQueue().ToUpper();
                        if (quest.QueueName.ToUpper().Equals(currentQueue, StringComparison.OrdinalIgnoreCase))
                        {
                            IncrementProgress(1, quest);
                            StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                            UpdateWatchText();
                            Debug.Log($"Current queue matches the quest requirement: {quest.QueueName.ToUpper()}");
                        }
                        else
                        {
                            Debug.Log($"This quest requires a different queue: {quest.QueueName.ToUpper()}. Current queue: {currentQueue}.");
                        }
                    }
                    else
                    {
                        Debug.Log("This quest does not require a specific queue.");
                    }
                }
            }
            else
            {
                Debug.Log("No room trigger quests available.");
            }
        }
        private int Streak
        {
            get { return _streak; }
            set
            {
                if (_streak != value)
                {
                    _streak = value;
                    OnStreakChanged();
                }
            }
        }

        private int previousInfectedCount;

        void CheckTagListChanges()
        {
            if (GorillaGameManager.instance is GorillaTagManager manager)
            {
                if (manager.currentInfected.Count != previousInfectedCount)
                {
                    if (!manager.isCurrentlyTag && manager.currentInfected.Count == PhotonNetwork.CurrentRoom.PlayerCount - 1 && !endofround)
                    {
                        Streak++;
                        endofround = true;
                    }
                }
                else
                {
                    if (manager.waitingToStartNextInfectionGame)
                    {
                        endofround = false;
                    }
                }
            }
        }
        private bool endofround = false;

        private int _streak;


        private void OnStreakChanged()
        {
            UpdateQuestProgress();
        }

        void UpdateQuestProgress()
        {
            if (Streak > 0)
            {
                Debug.Log("True 1");

                foreach (var quest in selectedQuests)
                {
                    var questsToCheck = new List<(string questName, int requiredStreak)>
            {
                ("Be last for 1 Game of Infection", 1),
                ("Be last 2 Rounds in a Row in Infection", 2),
                ("Be last 3 Rounds in a Row in Infection", 3)
            };
                    foreach (var (questName, requiredStreak) in questsToCheck)
                    {
                        IncrementProgressIfEligible(quest, questName, Streak);
                    }
                }
            }
        }

        private void IncrementProgressIfEligible(Quest quest, string questName, int streakIncrement)
        {

            if (quest.Name == questName)
            {
                SetProgress(quest, streakIncrement);
                StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                UpdateWatchText();
            }
        }

        private bool roundended = false;
        private int RoundsCompleted;
        void Update()
        {
            WatchUpdatingInput();
            if (PhotonNetwork.InRoom && GetCurrentGamemode() == "INFECTION")
            {
                CheckTagListChanges();
                if (GorillaGameManager.instance is GorillaTagManager manager)
                {
                    if (manager.currentInfected.Count == PhotonNetwork.CurrentRoom.PlayerCount && !roundended)
                    {
                        Debug.Log("Round ended, checking quests");

                        foreach (var quest in selectedQuests)
                        {
                            var questsToCheck = new List<string>
                {
                    "Complete 1 Round Of Infection",
                    "Complete 2 Rounds of Infection",
                    "Complete 5 Rounds Of Infection"
                };
                            if (questsToCheck.Contains(quest.Name))
                            {
                                IncrementProgress(1, quest);
                            }
                        }
                        roundended = true;
                        RoundsCompleted++;
                    }


                    if (manager.waitingToStartNextInfectionGame)
                    {
                        Debug.Log("New round starting, resetting round end flag.");
                        roundended = false;
                    }
                }
            }
            /* if (PhotonNetwork.InRoom)
             {

             }*/// CompleteQuest(selectedQuests.FirstOrDefault(q => q.Name == "Join a Lobby"), DetermineQuestType(selectedQuests.FirstOrDefault(q => q.Name == "Join a Lobby")));
            if (PhotonNetwork.InRoom && !Debounce)
            {
                Debounce = true;
                JoinedRoom = true;
                OnRoomJoin(PhotonNetwork.CurrentRoom.Name);
            }
            else if (!PhotonNetwork.InRoom && Debounce)
            {
                Debounce = false;
                JoinedRoom = false;
                if (timeTrackingCoroutine != null)
                {
                    StopCoroutine(timeTrackingCoroutine);
                    timeTrackingCoroutine = null;
                }

            }
        }
        private void InitializeQuests()
        {
            //---------------------Normal Quests---------------------
            quests.Add(new Quest("Play for 5 Minutes", 5, 5, true, false, false, null, false, null, false, null, QuestTypeCooldown.Normal)); // done
            quests.Add(new Quest("Complete 1 Round Of Infection", 1, 15, false, true, true, "Infection", false, null, false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Join a Lobby", 1, 5, false, true, false, null, false, null, false, null, QuestTypeCooldown.Normal)); // done
            quests.Add(new Quest("Play for 10 Minutes", 10, 10, true, false, false, null, false, null, false, null, QuestTypeCooldown.Normal)); // done
            quests.Add(new Quest("Play Casual Mode", 1, 5, false, true, true, "Casual", false, null, false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Infection Mode", 1, 5, false, true, true, "Infection", false, null, false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Hunt Mode", 1, 5, false, true, true, "Hunt", false, null, false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Competitive Queue", 1, 20, false, true, false, null, false, null, true, "Competitive", QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Default Queue", 1, 10, false, true, false, null, false, null, true, "Default", QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Minigames Queue", 1, 10, false, true, false, null, false, null, true, "Minigames", QuestTypeCooldown.Normal));// done
                                                                                                                                                     // Map-based quests (map is true, mapname must be provided)
            string[] maps = { "Forest", "Canyons", "Mountain", "City", "Beach", "Rotation", "Basement", "Metro" };
            string[] gameModes = { "Casual", "Infection", "Hunt" };

            foreach (string map in maps)
            {
                quests.Add(new Quest($"Play {map} for 5 Minutes", 5, 10, true, true, false, null, true, map, false, null, QuestTypeCooldown.Normal));
                foreach (string mode in gameModes)
                {
                    quests.Add(new Quest($"Play {map} in {mode} Mode", 5, 15, true, true, true, mode, true, map, false, null, QuestTypeCooldown.Normal));
                }
            }

            //---------------------Advanced Quests---------------------
            advancedQuests.Add(new Quest("Play for 20 Minutes", 20, 20, true, false, false, null, false, null, false, null, QuestTypeCooldown.Advanced)); // done
            advancedQuests.Add(new Quest("Complete 2 Rounds of Infection", 2, 25, false, false, false, null, false, null, false, null, QuestTypeCooldown.Advanced));// done

            foreach (string map in maps)
            {
                advancedQuests.Add(new Quest($"Play {map} for 10 Minutes", 10, 30, true, true, false, null, true, map, false, null, QuestTypeCooldown.Advanced));

                foreach (string mode in gameModes)
                {
                    advancedQuests.Add(new Quest($"Play {map} in {mode} Mode for 10 Minutes", 15, 65, true, true, true, mode, true, map, false, null, QuestTypeCooldown.Advanced));
                }
            }

            //---------------------Expert Quests---------------------
            expertQuests.Add(new Quest("Complete 5 Rounds Of Infection", 5, 50, false, true, true, "Infection", false, null, false, null, QuestTypeCooldown.Expert));// done
            expertQuests.Add(new Quest("Play for 30 Minutes", 30, 50, true, false, false, null, false, null, false, null, QuestTypeCooldown.Expert)); // done

            foreach (string map in maps)
            {
                expertQuests.Add(new Quest($"Play {map} for 15 Minutes", 15, 70, true, true, false, null, true, map, false, null, QuestTypeCooldown.Expert));

                foreach (string mode in gameModes)
                {
                    expertQuests.Add(new Quest($"Play {map} in {mode} Mode for 15 Minutes", 15, 120, true, true, true, mode, true, map, false, null, QuestTypeCooldown.Expert));
                }
            }
        }
        

                private bool showPopup = true;
                private Rect windowRect;
                private Vector2 originalButtonSize = new Vector2(100, 40);
                private Vector2 clickedButtonSize;
                void OnGUI()
                {
                    if (showPopup)
                    {
                        float windowWidth = 300;
                        float windowHeight = 200;
                        windowRect = new Rect((Screen.width - windowWidth) / 2, (Screen.height - windowHeight) / 2, windowWidth, windowHeight);

                        labelStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 16,
                            normal = { textColor = new Color(1f, 0.84f, 0f) },
                            fontStyle = FontStyle.Bold
                        };

                        buttonStyle = new GUIStyle(GUI.skin.button)
                        {
                            fontSize = 14,
                            normal = { background = MakeTex(2, 2, new Color(0.8f, 0.6f, 0f)) },
                            hover = { background = MakeTex(2, 2, new Color(0.6f, 0.4f, 0f)) }, 
                            active = { background = MakeTex(2, 2, new Color(0.5f, 0.3f, 0f)) }, 
                            fontStyle = FontStyle.Bold
                        };

                        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
                        boxStyle.normal.background = new Texture2D(1, 1);
                        boxStyle.normal.background.SetPixel(0, 0, new Color(0.5f, 0.5f, 0f));
                        boxStyle.normal.background.Apply();

                        GUILayout.BeginArea(windowRect);
                        GUI.Box(new Rect(0, 0, windowWidth, windowHeight), "", boxStyle);

                        GUILayout.Label("Join the Gorilla Quest's Discord to stay updated! -Tox", labelStyle);

                        if (GUILayout.Button("Join", buttonStyle, GUILayout.Height(50)))
                        {
                            Application.OpenURL("https://discord.gg/39fFSURGFQ");
                        }

                        if (GUILayout.Button("Dismiss", buttonStyle, GUILayout.Height(50)))
                        {
                            PlayerPrefs.SetInt("DiscordPopupDismissed", 1);
                            PlayerPrefs.Save();
                            showPopup = false;
                        }

                        GUILayout.EndArea();
                    }
                }

                private Texture2D MakeTex(int width, int height, Color col)
                {
                    Color[] pix = new Color[width * height];
                    for (int i = 0; i < pix.Length; i++)
                    {
                        pix[i] = col;
                    }
                    Texture2D result = new Texture2D(width, height);
                    result.SetPixels(pix);
                    result.Apply();
                    return result;
                }
        /*void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, Screen.height));
            foreach (var quest in selectedQuests)
            {
                if (GUILayout.Button($"Complete {quest.Name}"))
                {
                    try
                    {
                        CompleteQuest(quest, DetermineQuestType(quest));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error completing quest {quest.Name}: {ex.Message}");
                    }
                }
            }
            if (GUILayout.Button($"Add Streak"))
            {
                Streak++;
                StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                UpdateWatchText();
            }
            if (GUILayout.Button($"Reset Streak"))
            {
                Streak = 0;
                StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                UpdateWatchText();
            }
            if (GUILayout.Button("Reroll Quests"))
            {
                SelectRandomQuests();
                CheckAndStartCountQuest();
                StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                UpdateWatchText();

            }
            if (GUILayout.Button("Save Quests"))
            {
                SaveAllQuestProgress();
            }
            if (GUILayout.Button("Load Quests"))
            {
                LoadQuestProgress();
                StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                UpdateWatchText();
            }
            if (PhotonNetwork.InRoom)
            {
                if (GUILayout.Button("Leave Room"))
                {
                    PhotonNetwork.Disconnect();
                }
            }
            if (GUILayout.Button("Next Page"))
            {
                page++;
                if (page > 3)
                {
                    page = 1;
                }
                UpdateWatchText();
            }
            if (GUILayout.Button("Back Page"))
            {
                page--;

                if (page < 1)
                {
                    page = 3;
                }
                UpdateWatchText();
            }
            GUILayout.Label(FormatMap(MapPatch.ActiveZones.First().ToString().ToUpper()));
            GUILayout.EndArea();
        }*/



        void SelectRandomQuest(Quest completedQuest, QuestType questType)
        {
            if (selectedQuests.Contains(completedQuest))
            {
                selectedQuests.Remove(completedQuest);
                Quest newQuest = null;
                switch (questType)
                {
                    case QuestType.Normal:
                        newQuest = SelectWeightedQuest(questWeights, quests);
                        break;

                    case QuestType.Advanced:
                        newQuest = SelectWeightedQuest(advancedQuestWeights, advancedQuests);
                        break;

                    case QuestType.Expert:
                        newQuest = SelectWeightedQuest(expertQuestWeights, expertQuests);
                        break;

                    default:
                        Debug.LogWarning("Invalid quest type specified.");
                        break;
                }

                if (newQuest != null && !selectedQuests.Contains(newQuest))
                {
                    selectedQuests.Add(newQuest);
                    Debug.Log($"New Quest Added: {newQuest.Name}");
                }

                StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                UpdateWatchText();
            }
        }

        System.Random random = new System.Random();
        private Quest SelectWeightedQuest(Dictionary<Quest, int> questWeightDict, List<Quest> questList)
        {
            int totalWeight = questList.Sum(q => questWeightDict[q]);
            int randomNumber = random.Next(0, totalWeight);
            int cumulativeWeight = 0;

            foreach (var quest in questList)
            {
                cumulativeWeight += questWeightDict[quest];
                if (randomNumber < cumulativeWeight)
                {
                    return quest;
                }
            }

            return null;
        }

        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle windowStyle;
        public enum QuestType
        {
            Normal,
            Advanced,
            Expert
        }

        public void AddQuestProgress(string questName, int progressToAdd)
        {
            Quest quest = selectedQuests.FirstOrDefault(q => q.Name == questName);

            if (quest != null)
            {
                quest.Progress += progressToAdd;
                Debug.Log($"Added {progressToAdd} progress to quest: {quest.Name}. Current progress: {quest.Progress}/{quest.Goal}");
                if (quest.Progress >= quest.Goal)
                {
                    CompleteQuest(quest, DetermineQuestType(quest));
                }
            }
            else
            {
                Debug.LogWarning($"Quest '{questName}' not found.");
            }
        }

        private QuestType DetermineQuestType(Quest quest)
        {
            if (quests.Contains(quest))
            {
                return QuestType.Normal;
            }
            else if (advancedQuests.Contains(quest))
            {
                return QuestType.Advanced;
            }
            else if (expertQuests.Contains(quest))
            {
                return QuestType.Expert;
            }
            return QuestType.Normal;
        }

        private void CompleteQuest(Quest quest, QuestType questType)
        {
            switch (questType)
            {
                case QuestType.Normal:
                    CompleteNormalQuest(quest);
                    break;

                case QuestType.Advanced:
                    CompleteAdvancedQuest(quest);
                    break;

                case QuestType.Expert:
                    CompleteExpertQuest(quest);
                    break;

                default:
                    Debug.LogWarning("Invalid quest type specified.");
                    break;
            }

            SaveAllQuestProgress();
        }

        private Coroutine motdUpdateCoroutine;

        private void CompleteQuest(Quest quest, QuestTypeCooldown questType)
        {
            if (quest.CanPickup())
            {
                totalXP += quest.XP;
                quest.Progress = quest.Goal;
                quest.LastPickedUpTime = DateTime.Now;

                int index = selectedQuests.IndexOf(quest);
                if (index != -1)
                {
                    ResetProgress(selectedQuests[index]);
                    quest.IsOnCooldown = true;
                    StartCoroutine(CooldownTimer(quest, index, questType));

                    if (motdUpdateCoroutine != null)
                    {
                        StopCoroutine(motdUpdateCoroutine);
                    }
                    motdUpdateCoroutine = StartCoroutine(UpdateMOTDOnCooldown(quest));
                }
            }
            else
            {
                Debug.Log($"Quest '{quest.Name}' is still on cooldown.");
            }
        }

        private IEnumerator UpdateMOTDOnCooldown(Quest quest)
        {
            while (quest.IsOnCooldown)
            {
                StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                UpdateWatchText();
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator CooldownTimer(Quest quest, int index, QuestTypeCooldown questType)
        {
            CompleteQuestVFX((int)questType);
            StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
            UpdateWatchText();

            DateTime cooldownExpiration = quest.LastPickedUpTime + quest.CooldownDuration;
            yield return new WaitUntil(() => DateTime.Now >= cooldownExpiration);
            StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
            UpdateWatchText();
            quest.IsOnCooldown = false; ;
            selectedQuests[index] = GetRandomQuest(DetermineQuestTypeList(quest), selectedQuests);
            GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(completeeasy);
            CheckAndStartCountQuest();
        }

        private TimeSpan GetRemainingCooldown(Quest quest)
        {
            DateTime cooldownExpiration = quest.LastPickedUpTime + quest.CooldownDuration;
            TimeSpan remainingTime = cooldownExpiration - DateTime.Now;
            return remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;
        }

        private string GetRemainingCooldownString(Quest quest)
        {
            TimeSpan remainingTime = GetRemainingCooldown(quest);
            return $"{remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";
        }

        private List<Quest> DetermineQuestTypeList(Quest quest)
        {
            return quest.Type switch
            {
                QuestTypeCooldown.Normal => quests,
                QuestTypeCooldown.Advanced => advancedQuests,
                QuestTypeCooldown.Expert => expertQuests,
                _ => throw new ArgumentOutOfRangeException(nameof(quest), quest, null)
            };
        }

        private void CompleteNormalQuest(Quest quest) => CompleteQuest(quest, QuestTypeCooldown.Normal);
        private void CompleteAdvancedQuest(Quest quest) => CompleteQuest(quest, QuestTypeCooldown.Advanced);
        private void CompleteExpertQuest(Quest quest) => CompleteQuest(quest, QuestTypeCooldown.Expert);

        private Quest GetRandomQuest(List<Quest> questList, List<Quest> selectedQuests)
        {
            var availableQuests = questList.Where(q => !selectedQuests.Contains(q)).ToList();
            return availableQuests.OrderBy(q => Guid.NewGuid()).FirstOrDefault();
        }
        IEnumerator SetupMOTDWithDelay(string topTextContent)
        {
            if (motd == null)
            {
                GameObject motdThing = GameObject.Find("Environment Objects/LocalObjects_Prefab/TreeRoom/motd (1)");
                motd = Instantiate(motdThing, motdThing.transform.parent);
                motdThing.SetActive(false);
            }

            TextMeshPro topText = motd.GetComponent<TextMeshPro>();
            ConfigureText(topText, $"{topTextContent}  Streak: {Streak}   XP: {totalXP}", 80);

            if (motdText == null)
            {
                motdText = GameObject.Find("Environment Objects/LocalObjects_Prefab/TreeRoom/motdtext");
                motdText.GetComponent<PlayFabTitleDataTextDisplay>().enabled = false;
            }

            yield return new WaitForSeconds(0.2f);

            TextMeshPro bottomText = motdText.GetComponent<TextMeshPro>();
            bottomText.text = GetQuestText();
            bottomText.alignment = TextAlignmentOptions.Center;
            ConfigureText(bottomText, bottomText.text, 60);
        }
        private string blinkcolor;
        private string GetQuestText()
        {
            string questHeader = "Normal Quest";
            string advancedQuestHeader = "Advanced Quest";
            string expertQuestHeader = "Expert Quest";
            string questText = $"{questHeader}\n" +
                               "-----------------------------------------------------------\n";

            int maxLength = 60;

            void AddQuests(List<Quest> questList, string color)
            {
                string line = "";
                for (int i = 0; i < questList.Count; i++)
                {
                    var quest = questList[i];
                    string progressColor;

                    if (quest.IsOnCooldown)
                    {
                        TimeSpan remainingCooldown = GetRemainingCooldown(quest);
                        TimeSpan totalCooldown = quest.CooldownDuration;
                        if (remainingCooldown.TotalSeconds > totalCooldown.TotalSeconds * 2 / 3)
                        {
                            progressColor = "green";
                        }
                        else if (remainingCooldown.TotalSeconds > totalCooldown.TotalSeconds / 3)
                        {
                            progressColor = "yellow";
                        }
                        else
                        {
                            progressColor = blinkcolor;

                        }
                        string remainingTime = GetRemainingCooldownString(quest);
                        string questDisplayName = $"<color={progressColor}>Next Quest In: {remainingTime}</color>";
                        questText += line + "\n";
                        line = questDisplayName;
                    }
                    else
                    {
                        string questDisplayName = $"<color={color}> {quest.Name} [{quest.Progress}/{quest.Goal}]</color>";

                        if (line.Length + questDisplayName.Length + 5 > maxLength)
                        {
                            questText += line + "\n";
                            line = questDisplayName;
                        }
                        else
                        {
                            if (line.Length > 0)
                            {
                                line += "     ";
                            }
                            line += questDisplayName;
                        }
                    }
                }
                if (line.Length > 0)
                {
                    questText += line + "\n";
                }
            }

            questText = "";
            questText += $"{questHeader}\n" +
                         "-----------------------------------------------------------\n";

            var regularQuests = selectedQuests.Where(q => quests.Contains(q)).ToList();
            AddQuests(regularQuests, "yellow");

            questText += "\n" + $"{advancedQuestHeader}\n" +
                         "-----------------------------------------------------------\n";
            var advancedQuestList = selectedQuests.Where(q => advancedQuests.Contains(q)).ToList();
            AddQuests(advancedQuestList, "red");

            questText += "\n" + $"{expertQuestHeader}\n" +
                         "-----------------------------------------------------------\n";
            var expertQuestList = selectedQuests.Where(q => expertQuests.Contains(q)).ToList();
            AddQuests(expertQuestList, "purple");

            return questText;
        }

        private IEnumerator BlinkQuestText()
        {
            while (true)
            {
                blinkcolor = "red";
                yield return new WaitForSeconds(1f);
                blinkcolor = "white";
                yield return new WaitForSeconds(1f);
            }
        }
        void ConfigureText(TextMeshPro tmp, string text, float fontSize)
        {
            tmp.richText = true;
            tmp.fontSize = fontSize;
            tmp.text = text;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }
        private GameObject normalParticle;
        private GameObject advanceParticle;
        private GameObject expertParticle;

        void CompleteQuestVFX(int quest_type)
        {
            switch (quest_type)
            {
                case 0:
                    {
                        normalParticle.GetComponent<ParticleSystem>().Play();
                        GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(completeharder);
                        break;
                    }
                case 1:
                    {
                        advanceParticle.GetComponent<ParticleSystem>().Play();
                        GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(completeharder);
                        break;
                    }
                case 2:
                    {
                        expertParticle.GetComponent<ParticleSystem>().Play();
                        GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(completeharder);
                        break;
                    }
                default:
                    {
                        Debug.LogWarning("Invalid quest type.");
                        break;
                    }
            }
        }
        private AudioClip completeeasy;
        private AudioClip completeharder;
        private void CheckAndStartCountQuest()
        {
            try
            {
                var questsWithCount = selectedQuests.Where(q =>
                {
                    if (q == null)
                    {
                        Debug.LogWarning("Encountered a null quest in selectedQuests.");
                        return false;
                    }


                    return q.Count;
                }).ToList();

                foreach (var quest in questsWithCount)
                {
                    if (quest != null)
                    {
                        StartCoroutine(CountUpQuestProgress(quest));
                    }
                    else
                    {
                        Debug.LogWarning("Attempted to start a coroutine for a null quest.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred in CheckAndStartCountQuest: {ex.Message}");
            }
        }
        void UpdateWatchText()
        {
            Page1 = GenerateQuestDisplay("Normal Quests", QuestTypeCooldown.Normal, "yellow");
            Page2 = GenerateQuestDisplay("Advanced Quests", QuestTypeCooldown.Advanced, "red");
            Page3 = GenerateQuestDisplay("Expert Quests", QuestTypeCooldown.Expert, "purple");
            if (page == 1)
            {
                GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>().text.text = Page1;
            }
            if (page == 2)
            {
                GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>().text.text = Page2;
            }
            if (page == 3)
            {
                GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>().text.text = Page3;
            }

        }

        private string GenerateQuestDisplay(string title, QuestTypeCooldown questType, string color)
        {
            var quests = selectedQuests
                .Where(q => q.Type == questType)
                .Take(3)
                .ToList();

            StringBuilder questDisplay = new StringBuilder($"{title}\n<color={color}>");

            if (quests.Count > 0)
            {
                foreach (var quest in quests)
                {
                    if (taskMappings.TryGetValue(quest.Name, out string output))
                    {
                        questDisplay.Append($"{output} {quest.Progress}/{quest.Goal}\n");
                    }
                }
            }
            else
            {
                questDisplay.Append("No quest rn\n");
            }
            if (questType == QuestTypeCooldown.Expert)
            {
                questDisplay.Append($"</color>Page: {page}/3\nXP: {totalXP}");
            }
            else { 
                questDisplay.Append($"</color>Page: {page}/3");
             }
            return questDisplay.ToString();
        }

        private string ColorToHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }
        private string Page1;
        private string Page2;
        private string Page3;
        private int page = 1;
        void SetupWatch()
        {
            GorillaTagger.Instance.offlineVRRig.EnableHuntWatch(true);
            GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>().enabled = false;
            GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>().badge.gameObject.SetActive(false);
            GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>().leftHand.gameObject.SetActive(false);
            GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>().rightHand.gameObject.SetActive(false);
            GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>().hat.gameObject.SetActive(false);
            GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>().face.gameObject.SetActive(false);
            GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>().material.gameObject.SetActive(false);
            var huntComputer = GorillaTagger.Instance.offlineVRRig.huntComputer.GetComponent<GorillaHuntComputer>();
            huntComputer.text.alignment = TextAnchor.MiddleLeft;
            huntComputer.text.fontSize = 14;
            huntComputer.text.verticalOverflow = VerticalWrapMode.Overflow;
            huntComputer.text.horizontalOverflow = HorizontalWrapMode.Overflow;
            huntComputer.text.alignByGeometry = true;
            huntComputer.text.resizeTextForBestFit = false;
            huntComputer.text.resizeTextMinSize = 10;
            huntComputer.text.resizeTextMaxSize = 14;
            if (page == 1) 
            {
                huntComputer.text.text = Page1;
            }
            if (page == 2)
            {
                huntComputer.text.text = Page2;
            }
            if (page == 3)
            {
                huntComputer.text.text = Page3;
            }
        }
        public static float PageCoolDown;
        void WatchUpdatingInput()
        {
            if (ControllerInputPoller.instance.leftControllerSecondaryButton && Time.time > PageCoolDown + 0.5)
            {
                PageCoolDown = Time.time;
                page++;
                GorillaTagger.Instance.offlineVRRig.PlayHandTapLocal(67, true, 1f);
                UpdateWatchText();
            }
            if (ControllerInputPoller.instance.leftControllerPrimaryButton && Time.time > PageCoolDown + 0.5f)
            {
                PageCoolDown = Time.time;
                page--;
                GorillaTagger.Instance.offlineVRRig.PlayHandTapLocal(67, true, 1f);
                UpdateWatchText();
            }
            if (page > 3)
            {
                page = 1;
                UpdateWatchText();
            }
            if (page < 1)
            {
                page = 3;
                UpdateWatchText();
            }
        }
        [Obsolete]
        void playerSpawned()
        {
            PhotonNetworkController.Instance.disableAFKKick = true;
            if (PhotonNetwork.InRoom && GetCurrentGamemode() == "INFECTION")
            {
                if (GorillaGameManager.instance is GorillaTagManager manager)
                {
                    previousInfectedCount = manager.currentInfected.Count;
                }
            }
            InitializeQuests();
            InitializeQuestWeights();
            SetupWatch();
            UpdateWatchText();
            PhotonNetwork.NetworkingClient.EventReceived += EventReceived;
            gtmanager = GameObject.Find("GT Systems/GameModeSystem/Gorilla Tag Manager").GetComponent<GorillaTagManager>();
            LoadQuestProgress();
            StartCoroutine(BlinkQuestText());
            StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
            Debug.Log("Loaded");
            var bundle = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("Gorilla_Quests.Assets.gorillaquestsfx"));
            normalParticle = Instantiate(bundle.LoadAsset<GameObject>("normalparticle"));
            advanceParticle = Instantiate(bundle.LoadAsset<GameObject>("advancedparticle"));
            expertParticle = Instantiate(bundle.LoadAsset<GameObject>("expertparticle"));
            var headparent = GameObject.Find("Player Objects/Local VRRig/Local Gorilla Player/RigAnchor/rig/body/");
            SetParticlePosition(normalParticle, headparent.transform);
            SetParticlePosition(advanceParticle, headparent.transform);
            SetParticlePosition(expertParticle, headparent.transform);
            completeeasy = bundle.LoadAsset<AudioClip>("complete2");
            completeharder = bundle.LoadAsset<AudioClip>("complete");
            GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(completeeasy);
            GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(completeharder);
            if (normalParticle != null)
            {
                normalParticle.GetComponent<ParticleSystem>().Play();
                Debug.Log("Loaded Particle");
            }
            if (advanceParticle != null)
            {
                advanceParticle.GetComponent<ParticleSystem>().Play();
            }
            if (expertParticle != null)
            {
                expertParticle.GetComponent<ParticleSystem>().Play();
            }
            CheckAndStartCountQuest();
            
        }

        private void SetParticlePosition(GameObject particle, Transform parent)
        {
            if (particle != null)
            {
                particle.transform.position = Vector3.zero;
                particle.transform.SetParent(parent, false);
            }
        }
        private AssetBundle assetBundle;
        private static AssetBundle LoadAssetBundle(string path)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            if (stream == null)
            {
                Debug.LogError($"Failed to find resource stream for path: {path}");
                return null;
            }

            AssetBundle bundle = AssetBundle.LoadFromStream(stream);
            return bundle;
        }

        public AssetBundle GetAssetBundle()
        {
            if (assetBundle == null)
            {
                assetBundle = LoadAssetBundle("Gorilla_Quest.Assets.gorillaquestsfx");
            }
            return assetBundle;
        }

        private void SelectRandomQuests()
        {
            selectedQuests.Clear();
            var random = new System.Random();


            Quest SelectWeightedQuest(Dictionary<Quest, int> questWeightDict, List<Quest> questList)
            {
                int totalWeight = questList.Sum(q => questWeightDict[q]);
                int randomNumber = random.Next(0, totalWeight);
                int cumulativeWeight = 0;

                foreach (var quest in questList)
                {
                    cumulativeWeight += questWeightDict[quest];
                    if (randomNumber < cumulativeWeight)
                    {
                        return quest;
                    }
                }

                return null;
            }

            while (selectedQuests.Count < 3)
            {
                var quest = SelectWeightedQuest(questWeights, quests);
                if (quest != null && !selectedQuests.Contains(quest))
                {
                    selectedQuests.Add(quest);
                }
            }


            while (selectedQuests.Count < 6)
            {
                var quest = SelectWeightedQuest(advancedQuestWeights, advancedQuests);
                if (quest != null && !selectedQuests.Contains(quest))
                {
                    selectedQuests.Add(quest);
                }
            }

            int expertQuestCount = random.Next(1, 3);
            while (selectedQuests.Count < 6 + expertQuestCount)
            {
                var quest = SelectWeightedQuest(expertQuestWeights, expertQuests);
                if (quest != null && !selectedQuests.Contains(quest))
                {
                    selectedQuests.Add(quest);
                }
            }
        }


        private void IncrementProgress(int amount, Quest quest)
        {
            quest.Progress += amount;
            if (quest.Progress >= quest.Goal)
            {
                CompleteQuest(quest, DetermineQuestType(quest));
            }
        }

        private bool IsComplete(Quest quest)
        {
            return quest.Progress >= quest.Goal;
        }

        private void ResetProgress(Quest quest)
        {
            quest.Progress = 0;
        }
        private void SetProgress(Quest quest, int number)
        {
            quest.Progress = number;
            if (quest.Progress >= quest.Goal)
            {
                CompleteQuest(quest, DetermineQuestType(quest));
            }
        }

        private Dictionary<Quest, int> questWeights = new Dictionary<Quest, int>();
        private Dictionary<Quest, int> advancedQuestWeights = new Dictionary<Quest, int>();
        private Dictionary<Quest, int> expertQuestWeights = new Dictionary<Quest, int>();

        private void InitializeQuestWeights()
        {

            foreach (var quest in quests)
            {
                questWeights[quest] = GetWeightForQuest(quest);
            }

   
            foreach (var quest in advancedQuests)
            {
                advancedQuestWeights[quest] = GetWeightForAdvancedQuest(quest);
            }

            foreach (var quest in expertQuests)
            {
                expertQuestWeights[quest] = GetWeightForExpertQuest(quest);
            }
        }

        private int GetWeightForQuest(Quest quest)
        {
            if (quest.Map)
            {
                return 1;
            }
            return 10;
        }

        private int GetWeightForAdvancedQuest(Quest quest)
        {
            if (quest.Map)
            {
                return 1;
            }
            return 10;
        }

        private int GetWeightForExpertQuest(Quest quest)
        {
            if (quest.Map)
            {
                return 1;
            }
            return 10;
        }


    }
    public enum QuestTypeCooldown
    {
        Normal,
        Advanced,
        Expert
    }

    [Serializable]
    public class Quest
    {
        public string Name { get; set; }
        public int Goal { get; set; }
        public int XP { get; set; }
        public bool Count { get; set; }
        public int Progress { get; set; }
        public bool RoomTrigger { get; set; }
        public bool Gamemode { get; set; }
        public string GamemodeName { get; set; }
        public bool Map { get; set; }
        public string Mapname { get; set; }
        public bool Queue { get; set; }
        public string QueueName { get; set; }

        public DateTime LastPickedUpTime { get; set; }
        public QuestTypeCooldown Type { get; set; }

        public TimeSpan CooldownDuration
        {
            get
            {
                switch (Type)
                {
                    case QuestTypeCooldown.Normal:
                        return TimeSpan.FromSeconds(5);
                    case QuestTypeCooldown.Advanced:
                        return TimeSpan.FromSeconds(10);
                    case QuestTypeCooldown.Expert:
                        return TimeSpan.FromSeconds(15);
                    default:
                        return TimeSpan.Zero;
                }
            }
        }

        public bool CanPickup()
        {
            return DateTime.Now - LastPickedUpTime >= CooldownDuration;
        }

        public bool IsOnCooldown { get; set; }
        public void ResetCooldown()
        {
            IsOnCooldown = false;
        }
        public Quest(string name, int goal, int xp, bool count, bool roomtrigger, bool gamemode, string gamemodename, bool map, string mapname, bool queue, string queuename, QuestTypeCooldown questtype)
        {
            Name = name;
            Goal = goal;
            XP = xp;
            Count = count;
            Progress = 0;
            RoomTrigger = roomtrigger;
            Gamemode = gamemode;
            GamemodeName = gamemodename;
            Queue = queue;
            QueueName = queuename;
            Map = map;

            if (Map && string.IsNullOrEmpty(mapname))
            {
                throw new ArgumentException("Mapname is required when Map is true.");
            }

            Mapname = mapname;
            Type = questtype;
            LastPickedUpTime = DateTime.MinValue;
            IsOnCooldown = false;
        }
    }
}
