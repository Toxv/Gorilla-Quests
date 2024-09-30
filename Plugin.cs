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
        {            HarmonyPatches.ApplyHarmonyPatches();
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
            if (trueCount == 1 && quest.Count)
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
                }
                CompleteQuest(quest, DetermineQuestType(quest));
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
        string FormatMap(string map)// tehee wryser not stolen code
        {
            string formattedmap;
            switch (map)
            {
                case "CANYON":
                    formattedmap = "CANYONS";
                    break;
                case "CAVE":
                    formattedmap = "CAVES";
                    break;
                case "ROTATING":
                    formattedmap = "ROTATING";
                    break;
                default:
                    Debug.Log(map);
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
        private PropertyInfo[] boolProperties = typeof(Quest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(prop => prop.PropertyType == typeof(bool))
            .ToArray();
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
                Debug.Log("Has quests with room triggers");

                foreach (var quest in questsWithRoomTrigger)
                {

                    int trueCount = boolProperties.Count(prop => (bool)prop.GetValue(quest));

                    if (trueCount == 1 && quest.RoomTrigger)
                    {
                        print("true");
                        Debug.Log(quest);
                        IncrementProgress(1, quest);
                        StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
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
                        Debug.Log("This quest requires a count.");
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
                {           var questsToCheck = new List<(string questName, int requiredStreak)>
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
            }
        }

        private bool roundended = false;
        private int RoundsCompleted;
        void Update()
        {

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
            }
        }
        private void InitializeQuests()
        {
            //---------------------Normal Quests---------------------
            quests.Add(new Quest("Play for 5 Minutes", 5, 5, true, false, false, null, false, null, false, null, QuestTypeCooldown.Normal)); // done
            quests.Add(new Quest("Complete 1 Round Of Infection", 1, 15, false, true, true, "Infection", false, null, false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Join a Lobby", 1, 5, false, true, false, null, false, null, false, null, QuestTypeCooldown.Normal)); // done
                                                                                                                                       // quests.Add(new Quest("Tag 2 Players", 2, 20)); // commented out
            quests.Add(new Quest("Play for 10 Minutes", 10, 10, true, false, false, null, false, null, false, null, QuestTypeCooldown.Normal)); // done
            quests.Add(new Quest("Play Casual Mode", 1, 5, false, true, true, "Casual", false, null, false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Infection Mode", 1, 5, false, false, true, "Infection", false, null, false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Hunt Mode", 1, 5, false, true, true, "Hunt", false, null, false, null, QuestTypeCooldown.Normal));// done
            //quests.Add(new Quest("Join a Game Code", 1, 5, false, true, false, null, false, null, false, null, QuestTypeCooldown.Normal));

            // Map-based quests (map is true, mapname must be provided)
            quests.Add(new Quest("Play Forest", 1, 10, false, true, false, null, true, "Forest", false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Canyons", 1, 10, false, true, false, null, true, "Canyons", false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Mountains", 1, 10, false, true, false, null, true, "Mountains", false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play City", 1, 10, false, true, false, null, true, "City", false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Beach", 1, 10, false, true, false, null, true, "Beach", false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Rotation", 1, 10, false, true, false, null, true, "Rotation", false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Basement", 1, 10, false, true, false, null, true, "Basement", false, null, QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Metro", 1, 10, false, true, false, null, true, "Metro", false, null, QuestTypeCooldown.Normal));// done

            quests.Add(new Quest("Play Competitive Queue", 1, 20, false, true, false, null, false, null, true, "Competitive", QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Default Queue", 1, 10, false, true, false, null, false, null, true, "Default", QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Play Minigames Queue", 1, 10, false, true, false, null, false, null, true, "Minigames", QuestTypeCooldown.Normal));// done
            quests.Add(new Quest("Be last for 1 Game of Infection", 1, 20, false, true, true, "Infection", false, null, false, null, QuestTypeCooldown.Normal));// done

            //---------------------Advanced Quests---------------------
            advancedQuests.Add(new Quest("Play for 20 Minutes", 20, 20, true, false, false, null, false, null, false, null, QuestTypeCooldown.Advanced)); // done
            advancedQuests.Add(new Quest("Complete 2 Rounds of Infection", 2, 25, false, false, false, null, false, null, false, null, QuestTypeCooldown.Advanced));// done
            advancedQuests.Add(new Quest("Be last 2 Rounds in a Row in Infection", 2, 40, false, false, false, null, false, null, false, null, QuestTypeCooldown.Advanced));// done
           // advancedQuests.Add(new Quest("Avoid Being Tagged for 2 Minutes in Infection", 2, 35, true, false, false, null, false, null, false, null, QuestTypeCooldown.Advanced));
            //advancedQuests.Add(new Quest("Play Infection Mode for 20 Minutes", 20, 25, true, true, true, "Infection", false, null, false, null, QuestTypeCooldown.Advanced));

            //---------------------Expert Quests-----------------------
            expertQuests.Add(new Quest("Complete 5 Rounds Of Infection", 5, 50, false, true, true, "Infection", false, null, false, null, QuestTypeCooldown.Expert));// done
            expertQuests.Add(new Quest("Play for 30 Minutes", 30, 50, true, false, false, null, false, null, false, null, QuestTypeCooldown.Expert)); // done
            expertQuests.Add(new Quest("Play for 60 Minutes", 60, 120, true, false, false, null, false, null, false, null, QuestTypeCooldown.Expert)); // done
           // expertQuests.Add(new Quest("Avoid Being Tagged for 6 Minutes In Infection", 6, 35, true, true, true, "Infection", false, null, false, null, QuestTypeCooldown.Expert));
            expertQuests.Add(new Quest("Be last 3 Rounds in a Row In Infection", 3, 80, false, true, true, "Infection", false, null, false, null, QuestTypeCooldown.Expert));// done
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

                GUILayout.Label("Join the Gorilla Quest's Discord to stay updated!", labelStyle);

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
        /* void OnGUI()
         {
             GUILayout.BeginArea(new Rect(10, 10, 200, 300));
 /*            foreach (var quest in selectedQuests)
             {
                 if (GUILayout.Button($"Complete {quest.Name}"))
                 {
                     CompleteQuest(quest, DetermineQuestType(quest));
                 }
             }
             if (GUILayout.Button($"Add Streak"))
             {
                 Streak++;
                 StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
             }
             if (GUILayout.Button($"Reset Streak"))
             {
                 Streak = 0;
                 StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
             }
             if (GUILayout.Button("Reroll Quests"))
             {
                 SelectRandomQuests();
                 StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                 CheckAndStartCountQuest();
             }
             if (GUILayout.Button("Save Quests"))
             {
                 SaveAllQuestProgress();
             }
             if (GUILayout.Button("Load Quests"))
             {
                 LoadQuestProgress();
                 StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
             }
             if (GUILayout.Button("Leave Room"))
             {
                 PhotonNetwork.Disconnect();
             }
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
                            newQuest = quests.OrderBy(q => Guid.NewGuid()).FirstOrDefault();
                            if (newQuest != null)
                            {
                                selectedQuests.Add(newQuest);
                                Debug.Log($"New Quest Added: {newQuest.Name}");
                            }
                            break;

                        case QuestType.Advanced:
                            newQuest = advancedQuests.OrderBy(q => Guid.NewGuid()).FirstOrDefault();
                            if (newQuest != null)
                            {
                                selectedQuests.Add(newQuest);
                                Debug.Log($"New Advanced Quest Added: {newQuest.Name}");
                            }
                            break;

                        case QuestType.Expert:
                            newQuest = expertQuests.OrderBy(q => Guid.NewGuid()).FirstOrDefault();
                            if (newQuest != null)
                            {
                                selectedQuests.Add(newQuest);
                                Debug.Log($"New Expert Quest Added: {newQuest.Name}");
                            }
                            break;

                        default:
                            Debug.LogWarning("Invalid quest type specified.");
                            break;
                    }

                    StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));
                }
           
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
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator CooldownTimer(Quest quest, int index, QuestTypeCooldown questType)
        {
            CompleteQuestVFX((int)questType);
            StartCoroutine(SetupMOTDWithDelay("Gorilla Quests"));

            DateTime cooldownExpiration = quest.LastPickedUpTime + quest.CooldownDuration;
            yield return new WaitUntil(() => DateTime.Now >= cooldownExpiration);

            quest.IsOnCooldown = false;;
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
            var questsWithCount = selectedQuests.Where(q => q.Count).ToList();

            foreach (var quest in questsWithCount)
            {
                StartCoroutine(CountUpQuestProgress(quest));
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
            var randomQuests = quests
                .Where(q => !selectedQuests.Contains(q))
                .OrderBy(q => random.Next())
                .Take(3);

            selectedQuests.AddRange(randomQuests);
            var randomAdvancedQuests = advancedQuests
                .Where(q => !selectedQuests.Contains(q))
                .OrderBy(q => random.Next())
                .Take(3);

            selectedQuests.AddRange(randomAdvancedQuests);
            int expertQuestCount = random.Next(1, 3);
            var randomExpertQuests = expertQuests
                .Where(q => !selectedQuests.Contains(q))
                .OrderBy(q => random.Next())
                .Take(expertQuestCount);

            selectedQuests.AddRange(randomExpertQuests);
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
                        return TimeSpan.FromSeconds(3);
                    case QuestTypeCooldown.Advanced:
                        return TimeSpan.FromSeconds(3);
                    case QuestTypeCooldown.Expert:
                        return TimeSpan.FromSeconds(3);
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