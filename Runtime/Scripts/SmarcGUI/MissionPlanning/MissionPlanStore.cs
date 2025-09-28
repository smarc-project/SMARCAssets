using UnityEngine;
using TMPro;
using System.Collections.Generic;

using System.IO;
using System;
using Newtonsoft.Json;
using UnityEngine.UI;
using System.Collections;
using System.Text.RegularExpressions;
using SmarcGUI.MissionPlanning.Tasks;
using SmarcGUI.MissionPlanning.Params;
using SmarcGUI.Connections;




namespace SmarcGUI.MissionPlanning
{
    [RequireComponent(typeof(GUIState))]
    public class MissionPlanStore : MonoBehaviour
    {
        GUIState guiState;

        [Tooltip("Path to store mission plans")]
        public string MissionStoragePath;
        public List<TaskSpecTree> MissionPlans = new();

        [Header("Misison GUI Elements")]
        public Transform MissionsScrollContent;
        public Button NewMissionPlanButton;
        public Button LoadMissionsButton;
        public Button SaveMissionsButton;
        

        [Header("Mission Control Elements")]
        public Button RunMissionButton;
        public TMP_Dropdown MissionSignalsDropdown;
        public Button MissionSignalButton;


        [Header("Tasks GUI Elements")]
        public Transform TasksScrollContent;
        public TMP_Dropdown TaskTypeDropdown;
        public Button AddTaskButton;


        [Header("Prefabs")]
        public GameObject TSTPrefab;
        public GameObject TaskPrefab;
        public GameObject PrimitiveParamPrefab;
        public GameObject GeoPointParamPrefab;
        public GameObject LatLonParamPrefab;
        public GameObject OrientationParamPrefab;
        public GameObject ListParamPrefab;
        public GameObject DepthParamPrefab;
        public GameObject AltitudeParamPrefab;
        public GameObject HeadingParamPrefab;
        public GameObject AuvDepthPointParamPrefab;
        public GameObject AuvAltitudePointParamPrefab;
        public GameObject AuvHydrobaticPointParamPrefab;

        [Header("State of mission planning GUI")]
        public TSTGUI SelectedTSTGUI;

        Dictionary<string, Type> TaskTypes;
        Dictionary<string, string> TaskKebabToCamelCase;


        void Awake()
        {
            guiState = GetComponent<GUIState>();
            NewMissionPlanButton.onClick.AddListener(OnNewTST);
            LoadMissionsButton.onClick.AddListener(LoadMissionPlans);
            SaveMissionsButton.onClick.AddListener(SaveMissionPlans);
            RunMissionButton.onClick.AddListener(() => guiState.SelectedRobotGUI.SendStartTSTCommand(SelectedTSTGUI.tst));
            MissionSignalsDropdown.ClearOptions();
            MissionSignalsDropdown.AddOptions(new List<string>
            {
                WaspSignals.ENOUGH,
                WaspSignals.CONTINUE,
                WaspSignals.PAUSE,
                WaspSignals.ABORT,
                SmarcSignals.CANCEL_ABORT
            });
            MissionSignalButton.onClick.AddListener(() => guiState.SelectedRobotGUI.SendSignalTSTUnitCommand(MissionSignalsDropdown.options[MissionSignalsDropdown.value].text));
            AddTaskButton.onClick.AddListener(AddNewTask);

            // this finds all task types in the assembly through reflection.
            TaskTypes ??= Task.GetAllKnownTaskTypes();
            TaskKebabToCamelCase = new Dictionary<string, string>();
            foreach (var taskType in TaskTypes)
            {
                // convert the task name to kebab-case
                var kebabCaseName = Regex.Replace(taskType.Key, "([a-z0-9])([A-Z])", "$1-$2").ToLower();
                TaskKebabToCamelCase[kebabCaseName] = taskType.Key;
            }
            TaskTypeDropdown.ClearOptions();
            var taskKebabNames = new List<string>(TaskKebabToCamelCase.Keys);
            TaskTypeDropdown.AddOptions(taskKebabNames);
        }

        string ConvertDashToCamelCase(string input)
        {
            var camelized = Regex.Replace(input, "-.", m => m.Value.ToUpper()[1..]);
            return char.ToUpper(camelized[0]) + camelized[1..];
        }

        public void AddNewTask()
        {
            SelectedTSTGUI.OnTaskAdded(new TaskSpec(
                TaskKebabToCamelCase[TaskTypeDropdown.options[TaskTypeDropdown.value].text], // convert from kebab to camel to match c# class names
                null));
        }

        public Task CreateTask(string taskName)
        {
            TaskTypes ??= Task.GetAllKnownTaskTypes();
            if(!TaskTypes.ContainsKey(taskName))
            {
                // taskName could be in the waraps format too...
                // convert the name to CamelCase from kebab-case
                taskName = ConvertDashToCamelCase(taskName);
                if(!TaskTypes.ContainsKey(taskName))
                {
                    guiState.Log($"Task type {taskName} not defined in the GUI! Creating a custom task instead!");
                    taskName = "CustomTask";
                }
            }
            return (Task)Activator.CreateInstance(TaskTypes[taskName]);
        }

        void Start()
        {
            // Documents on win, user home on linux/mac
            MissionStoragePath = Path.Combine(GUIState.GetStoragePath(), "MissionPlans");
            Directory.CreateDirectory(MissionStoragePath);
        }


        void LateUpdate()
        {
            var missionInteraction = SelectedTSTGUI != null &&
                                            guiState.SelectedRobotGUI != null &&
                                            guiState.SelectedRobotGUI.TSTExecInfoReceived;
                                            
            RunMissionButton.interactable = missionInteraction;
            MissionSignalsDropdown.interactable = missionInteraction;
            MissionSignalButton.interactable = missionInteraction;

            AddTaskButton.interactable = SelectedTSTGUI != null;
        }


        void LoadMissionPlans()
        {
            var existingPlans = new Dictionary<string, TaskSpecTree>();
            foreach(var plan in MissionPlans)
            {
                existingPlans[plan.GetKey()] = plan;
            }

            var i=0;
            foreach (var file in Directory.GetFiles(MissionStoragePath))
            {
                if(!file.EndsWith(".json")) continue;
                var json = File.ReadAllText(file);
                try
                {
                    var plan = JsonConvert.DeserializeObject<TaskSpecTree>(json);
                    // Json does not know about _classes_ so we need to recover the types
                    // by checking for simple fields, and matching them to known classes
                    // Most of the work is done in the Task class
                    plan.RecoverFromJson();
                    if(existingPlans.ContainsKey(plan.GetKey()))
                    {
                        guiState.Log($"Skipping existing mission plan:{plan.GetKey()}. If you want to load this from file, either delete or modify the description of the one in the GUI.");
                        continue;
                    }
                    MissionPlans.Add(plan);
                    var tstGUI = Instantiate(TSTPrefab, MissionsScrollContent).GetComponent<TSTGUI>();
                    tstGUI.SetTST(plan);
                    i++;
                }
                catch (Exception e)
                {
                    guiState.Log($"Failed to load mission plan from {file}! Check debug logs.");
                    Debug.LogError(e);
                    continue;
                }
            }
            guiState.Log($"Loaded {i} mission plans");
        }

        void SaveMissionPlans()
        {
            var i=0;
            foreach (var plan in MissionPlans)
            {
                var json = JsonConvert.SerializeObject(plan, Formatting.Indented);
                var path = Path.Combine(MissionStoragePath, $"{plan.GetKey()}.json");
                File.WriteAllText(path, json);
                i++;
            }
            guiState.Log($"Saved {i} mission plans");
        }


        public void OnNewTST()
        {
            var newPlan = new TaskSpecTree();
            MissionPlans.Add(newPlan);
            var tstGUI = Instantiate(TSTPrefab, MissionsScrollContent).GetComponent<TSTGUI>();
            tstGUI.SetTST(newPlan);
            tstGUI.Select();
        }

        public void OnTSTDelete(TaskSpecTree tst)
        {
            var index = MissionPlans.IndexOf(tst);
            if (index >= 0 && index < MissionsScrollContent.childCount)
            {
                MissionPlans.RemoveAt(index);
                Destroy(MissionsScrollContent.GetChild(index).gameObject);
            }
        }

        public void OnTSTUp(TaskSpecTree tst)
        {
            var index = MissionPlans.IndexOf(tst);
            if(index == 0) return;
            MissionPlans.RemoveAt(index);
            MissionPlans.Insert(index-1, tst);
            // Swap the two TaskGUI objects
            var tstGO = MissionsScrollContent.GetChild(index).gameObject;
            var prevTSTGO = MissionsScrollContent.GetChild(index - 1).gameObject;
            tstGO.transform.SetSiblingIndex(index - 1);
            prevTSTGO.transform.SetSiblingIndex(index);
        }

        public void OnTSTDown(TaskSpecTree tst)
        {
            var index = MissionPlans.IndexOf(tst);
            if(index == MissionPlans.Count-1) return;
            MissionPlans.RemoveAt(index);
            MissionPlans.Insert(index+1, tst);
            // Swap the two TaskGUI objects
            var tstGO = MissionsScrollContent.GetChild(index).gameObject;
            var nextTSTGO = MissionsScrollContent.GetChild(index + 1).gameObject;
            tstGO.transform.SetSiblingIndex(index + 1);
            nextTSTGO.transform.SetSiblingIndex(index);
        }


        public void OnTSTSelected(TSTGUI tstGUI)
        {
            foreach(Transform child in MissionsScrollContent)
            {
                var tst = child.GetComponent<TSTGUI>();
                if(tst != tstGUI) tst.Deselect();
            }
            SelectedTSTGUI = tstGUI;
        }

        

        public GameObject GetParamPrefab(object paramValue)
        {
            return paramValue switch
            {
                string or int or float or bool or double or long => PrimitiveParamPrefab,
                GeoPoint => GeoPointParamPrefab,
                IList => ListParamPrefab,
                LatLon => LatLonParamPrefab,
                Orientation => OrientationParamPrefab,
                Depth => DepthParamPrefab,
                Altitude => AltitudeParamPrefab,
                Heading => HeadingParamPrefab,
                AuvDepthPoint => AuvDepthPointParamPrefab,
                AuvAltitudePoint => AuvAltitudePointParamPrefab,
                AuvHydrobaticPoint => AuvHydrobaticPointParamPrefab,
                _ => PrimitiveParamPrefab,
            };
        }
        

        

    }
}