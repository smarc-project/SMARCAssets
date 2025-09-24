using System.Collections.Generic;
using System.Linq;
using System;
using GeoRef;
using SmarcGUI.Connections;
using SmarcGUI.MissionPlanning;
using SmarcGUI.MissionPlanning.Tasks;
using SmarcGUI.MissionPlanning.Params;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SmarcGUI.KeyboardControllers;
using DefaultNamespace;
using SmarcGUI.WorldSpace;


namespace SmarcGUI
{
    public enum InfoSource
    {
        SIM,
        MQTT,
        ROS
    }


    public class RobotGUI : MonoBehaviour, IPointerClickHandler, IPointerExitHandler, IPointerEnterHandler, ICameraLookable
    {
        [Header("Params")]
        [Tooltip("Time in seconds before the robot is considered old")]
        public float OldnessTime = 10;

        [Header("UI Elements")]
        public RectTransform HighlightRT;
        public RectTransform SelectedHighlightRT;
        public RectTransform HeartRT;
        public TMP_Text RobotNameText;
        public TMP_Text InfoSourceText;
        public TMP_Dropdown TasksAvailableDropdown;
        public Button AddTaskButton;
        public RectTransform AvailTasksPanelRT;
        public Toggle UserInputToggle;
        public string WorldMarkerName = "WorldMarkers";
        public RectTransform ExecutingTasksScrollContent;
        public RectTransform ExecTasksPanelRT;
        public Button PingButton;
        TMP_Text PingButtonText;
        public Button AbortButton;

        [Header("Prefabs")]
        public GameObject ExecutingTaskPrefab;
        public GameObject RobotGUIOverlayPrefab;

        [Header("Ghost Prefabs")]
        public GameObject GenericGhostPrefab;
        public GameObject SAMGhostPrefab;
        public GameObject EvoloGhostPrefab;
        public GameObject LoloGhostPrefab;
        public GameObject PuffinGhostPrefab;

        WaspBasics waspBasics;

        Transform worldMarkersTF;
        Transform ghostTF;
        RobotGhost ghost;
        GameObject simRobotGO;
        Transform simRobotBaseLinkTF;


        public InfoSource InfoSource { get; private set; }
        WaspDirectExecutionInfoMsg directExecutionInfo;
        List<TaskSpec> tasksAvailable => directExecutionInfo.TasksAvailable;
        public List<string> TasksAvailableNames = new();
        public HashSet<string> TasksExecutingUuids = new();

        public string AgentUuid { get; private set; }

        public bool TSTExecInfoReceived = false;

        public string RobotName => RobotNameText.text;
        string robotNamespace;

        public bool IsSelected { get; private set; }
        GUIState guiState;
        MQTTClientGUI mqttClient;
        GlobalReferencePoint globalReferencePoint;
        MissionPlanStore missionPlanStore;
        RectTransform rt;
        float minHeight;
        float lastHeartbeatTime = -1;
        Color originalColor;
        Image BGImage;
        bool isOld = false;
        GameObject robotOverlayGO;
        KeyboardControllerBase keyboardController;
        const int MaxAbortCount = 5;
        int abortCount = MaxAbortCount;
        float lastAbortClick = 0;

        void Awake()
        {
            guiState = FindFirstObjectByType<GUIState>();
            mqttClient = FindFirstObjectByType<MQTTClientGUI>();
            missionPlanStore = FindFirstObjectByType<MissionPlanStore>();

            worldMarkersTF = GameObject.Find(WorldMarkerName).transform;
            globalReferencePoint = FindFirstObjectByType<GlobalReferencePoint>();
            AddTaskButton.onClick.AddListener(() => OnTaskAdded(TasksAvailableDropdown.value));
            PingButton.onClick.AddListener(SendPing);
            AbortButton.onClick.AddListener(SendAbort);
            rt = GetComponent<RectTransform>();
            minHeight = rt.sizeDelta.y;
            AvailTasksPanelRT.gameObject.SetActive(false);
            ExecTasksPanelRT.gameObject.SetActive(false);
            UserInputToggle.gameObject.SetActive(false);

            BGImage = GetComponent<Image>();
            originalColor = BGImage.color;
        }


        public void SetRobot(string robotname, InfoSource infoSource, string robotNamespace)
        {
            InfoSource = infoSource;
            this.robotNamespace = robotNamespace;

            RobotNameText.text = robotname;
            InfoSourceText.text = $"({infoSource})";

            if (infoSource == InfoSource.SIM)
            {
                HeartRT.gameObject.SetActive(false);
                UserInputToggle.gameObject.SetActive(true);


                simRobotGO = GameObject.Find(robotname);
                simRobotBaseLinkTF = Utils.FindDeepChildWithName(simRobotGO, "base_link").transform;

                keyboardController = simRobotGO.GetComponent<KeyboardControllerBase>();

                PingButton.gameObject.SetActive(false);
                AbortButton.gameObject.SetActive(false);

                minHeight = rt.sizeDelta.y - AbortButton.GetComponent<RectTransform>().sizeDelta.y;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, minHeight);
            }

            if (infoSource == InfoSource.MQTT)
            {
                mqttClient.SubToTopic(robotNamespace + "tst_execution_info");
                mqttClient.SubToTopic(robotNamespace + "direct_execution_info");
                mqttClient.SubToTopic(robotNamespace + "sensor_info");
                mqttClient.SubToTopic(robotNamespace + "exec/command");
                mqttClient.SubToTopic(robotNamespace + "exec/response");
                mqttClient.SubToTopic(robotNamespace + "exec/feedback");
                AvailTasksPanelRT.gameObject.SetActive(true);
                ExecTasksPanelRT.gameObject.SetActive(true);
                PingButton.gameObject.SetActive(true);
                PingButtonText = PingButton.GetComponentInChildren<TMP_Text>();
                PingButtonText.text = "Ping!";
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, minHeight + AvailTasksPanelRT.sizeDelta.y + ExecTasksPanelRT.sizeDelta.y);
                HeartRT.gameObject.SetActive(true);
            }

            if (infoSource != InfoSource.SIM && worldMarkersTF != null)
            {
                if (robotname.Contains("sam", StringComparison.InvariantCultureIgnoreCase)) ghostTF = Instantiate(SAMGhostPrefab).transform;
                else if (robotname.Contains("evolo", StringComparison.InvariantCultureIgnoreCase)) ghostTF = Instantiate(EvoloGhostPrefab).transform;
                else if (robotname.Contains("lolo", StringComparison.InvariantCultureIgnoreCase)) ghostTF = Instantiate(LoloGhostPrefab).transform;
                else if (robotname.Contains("puffin", StringComparison.InvariantCultureIgnoreCase)) ghostTF = Instantiate(PuffinGhostPrefab).transform;
                else
                {
                    guiState.Log($"No specific ghost prefab for {robotname}, using generic arrow.");
                    ghostTF = Instantiate(GenericGhostPrefab).transform;
                }

                ghostTF.name = $"Remote {robotname}";
                ghostTF.SetParent(worldMarkersTF);
                ghostTF.gameObject.SetActive(false);
                ghost = ghostTF.GetComponent<RobotGhost>();

                robotOverlayGO = Instantiate(RobotGUIOverlayPrefab);
                robotOverlayGO.name = $"{robotname}_Overlay";
                var robotOverlay = robotOverlayGO.GetComponent<RobotGUIOverlay>();
                robotOverlay.SetRobot(ghostTF, infoSource);
                robotOverlay.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
            }
        }


        /////////////////////////////////////////
        // MQTT STUFF
        /////////////////////////////////////////
        public void SendPing()
        {
            var pingCommand = new PingCommand();
            switch (InfoSource)
            {
                case InfoSource.SIM:
                    guiState.Log($"Ping! -> {RobotName} in SIM");
                    break;
                case InfoSource.MQTT:
                    mqttClient.Publish(robotNamespace + "exec/command", pingCommand.ToJson());
                    break;
                case InfoSource.ROS:
                    guiState.Log($"Ping! -> {RobotName} in ROS");
                    break;
            }
        }

        public void SendAbort()
        {
            abortCount--;
            AbortButton.GetComponentInChildren<TMP_Text>().text = $"ABORT ({abortCount})";
            lastAbortClick = Time.time;
            if (abortCount <= 0)
            {
                abortCount = MaxAbortCount;
                switch (InfoSource)
                {
                    case InfoSource.SIM:
                        guiState.Log($"ABORT! -> {RobotName} in SIM");
                        break;
                    case InfoSource.MQTT:
                        SendSignalTSTUnitCommand(WaspSignals.ABORT);
                        break;
                    case InfoSource.ROS:
                        guiState.Log($"ABORT! -> {RobotName} in ROS");
                        break;
                }
            }
        }


        public void SendSignalTaskCommand(string taskUuid, string signal)
        {
            var signalCommand = new SigntalTaskCommand(taskUuid: taskUuid, signal: signal);
            switch (InfoSource)
            {
                case InfoSource.SIM:
                    guiState.Log($"Sending signal {signal} to {RobotName} in SIM");
                    break;
                case InfoSource.MQTT:
                    mqttClient.Publish(robotNamespace + "exec/command", signalCommand.ToJson());
                    break;
                case InfoSource.ROS:
                    guiState.Log($"Sending signal {signal} to {RobotName} in ROS");
                    break;
            }
        }

        public StartTaskCommand SendStartTaskCommand(Task task)
        {
            var startTaskCommand = new StartTaskCommand(task, RobotName);
            switch (InfoSource)
            {
                case InfoSource.SIM:
                    guiState.Log($"Sending StartTaskCommand {task} to {RobotName} in SIM");
                    break;
                case InfoSource.MQTT:
                    mqttClient.Publish(robotNamespace + "exec/command", startTaskCommand.ToJson());
                    break;
                case InfoSource.ROS:
                    guiState.Log($"Sending StartTaskCommand {task} to {RobotName} in ROS");
                    break;
            }
            return startTaskCommand;
        }

        public void SendSignalTSTUnitCommand(string signal)
        {
            var signalTSTCommand = new SignalTSTUnit(signal, RobotName);
            switch (InfoSource)
            {
                case InfoSource.SIM:
                    guiState.Log($"Sending SignalTSTCommand {signal} to {RobotName} in SIM");
                    break;
                case InfoSource.MQTT:
                    mqttClient.Publish(robotNamespace + "tst/command", signalTSTCommand.ToJson());
                    break;
                case InfoSource.ROS:
                    guiState.Log($"Sending SignalTSTCommand {signal} to {RobotName} in ROS");
                    break;
            }
        }

        public StartTSTCommand SendStartTSTCommand(TaskSpecTree tst)
        {
            var startTSTCommand = new StartTSTCommand(tst, RobotName);
            switch (InfoSource)
            {
                case InfoSource.SIM:
                    guiState.Log($"Sending StartTSTCommand {tst} to {RobotName} in SIM");
                    break;
                case InfoSource.MQTT:
                    mqttClient.Publish(robotNamespace + "tst/command", startTSTCommand.ToJson());
                    break;
                case InfoSource.ROS:
                    guiState.Log($"Sending StartTSTCommand {tst} to {RobotName} in ROS");
                    break;
            }
            return startTSTCommand;
        }

        public void OnHeartbeatReceived(WaspHeartbeatMsg msg)
        {
            HeartRT.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            lastHeartbeatTime = Time.time;
            AgentUuid = msg.AgentUuid;
        }

        public void OnSensorInfoReceived(WaspSensorInfoMsg msg)
        {
            foreach (string sensorName in msg.SensorDataProvided)
            {
                mqttClient.SubToTopic(robotNamespace + "sensor/" + sensorName);
            }
        }


        void UpdateTasksDropdown(WaspDirectExecutionInfoMsg msg)
        {
            var tasksAvailable = msg.TasksAvailable;
            TasksAvailableDropdown.options.Clear();
            foreach (TaskSpec taskSpec in tasksAvailable)
            {
                TasksAvailableDropdown.options.Add(new TMP_Dropdown.OptionData() { text = taskSpec.Name });
            }
            TasksAvailableDropdown.RefreshShownValue();
        }

        void UpdateExecutingTasks(WaspDirectExecutionInfoMsg msg)
        {
            // bunch of loops here, but usually the item count is < 3 for all of them
            var newTasks = msg.TasksExecuting;

            HashSet<string> newUuids = new();
            foreach (var task in newTasks)
            {
                if (!task.ContainsKey("task-uuid"))
                {
                    guiState.Log($"Task executing on robot {RobotName} has no task-uuid!");
                    guiState.Log($"Task has keys: {task.Keys.Aggregate((acc, next) => acc + ", " + next)}");
                    continue;
                }
                var taskUuid = task["task-uuid"];
                newUuids.Add(taskUuid);
            }

            // old tasks that arent executing anymore
            var outdatedUuids = TasksExecutingUuids.Except(newUuids);
            // new tasks that are now executing, that werent before
            var newUuidsSet = newUuids.Except(TasksExecutingUuids);

            // nuke outdated tasks
            foreach (var taskUuid in outdatedUuids)
            {
                var index = TasksExecutingUuids.ToList().IndexOf(taskUuid);
                Destroy(ExecutingTasksScrollContent.GetChild(index).gameObject);
            }

            // create new ones
            foreach (var taskUuid in newUuidsSet)
            {
                // hardcoded strings coming from waraps api.
                var task = newTasks.Find(t => t["task-uuid"] == taskUuid);
                var taskName = task["task-name"];
                string taskDesc = task.ContainsKey("description") ? task["description"] : "No description";
                var execTaskGO = Instantiate(ExecutingTaskPrefab, ExecutingTasksScrollContent);
                var execTaskGUI = execTaskGO.GetComponent<ExecutingTaskGUI>();
                var taskSpec = msg.TasksAvailable.Find(t => t.Name == taskName);
                List<string> signals = new();
                if (taskSpec != null) signals = new List<string>(taskSpec.Signals);
                // abort must be available for tasks that have not been defined in tasks-available by the vehicle
                // as a fallback, so that the user can always stop a task.
                if (signals.Count == 0)
                {
                    guiState.Log($"No signals available for robot::task: {RobotName}::{taskName}, adding $abort as a fallback!");
                    signals.Add(WaspSignals.ABORT);
                }
                execTaskGUI.SetExecTask(this, taskName, taskDesc, taskUuid, signals);
            }

            TasksExecutingUuids = newUuids;
        }

        public void OnDirectExecutionInfoReceived(WaspDirectExecutionInfoMsg msg)
        {
            UpdateTasksDropdown(msg);
            UpdateExecutingTasks(msg);
            directExecutionInfo = msg;

            TasksAvailableNames = msg.TasksAvailable.Select(t => t.Name).ToList();
        }

        public void OnTSTExecutionInfoReceived(WaspTSTExecutionInfoMsg msg)
        {
            // the information about the executing TST is a different message sent
            // as a reply to the start-command. so this is literally just
            // a "yes we can start"...
            TSTExecInfoReceived = true;
            return;
        }


        public void OnPositionReceived(GeoPoint pos)
        {
            if (globalReferencePoint == null) return;
            if (ghostTF == null) return;
            if (pos.latitude == 0 && pos.longitude == 0) return;
            if (ghostTF.gameObject.activeSelf == false) ghostTF.gameObject.SetActive(true);

            var (x, z) = globalReferencePoint.GetUnityXZFromLatLon(pos.latitude, pos.longitude);
            ghost.UpdatePosition(new Vector3(x, pos.altitude, z));
        }

        public void OnHeadingReceived(float heading)
        {
            ghost.UpdateHeading(heading);
        }

        public void OnPitchReceived(float pitch)
        {
            ghost.UpdatePitch(pitch);
        }

        public void OnRollReceived(float roll)
        {
            ghost.UpdateRoll(-roll);
        }

        public void OnCourseReceived(float course)
        {
            ghost.UpdateCourse(course);
        }

        public void OnSpeedReceived(float speed)
        {
            ghost.UpdateSpeed(speed);
        }

        public void OnPingCmdReceived(PingCommand pingCmd)
        {
            var pongResponse = new PongResponse(pingCmd);
            mqttClient.Publish(robotNamespace + "exec/response", pongResponse.ToJson());
        }

        public void OnPongResponseReceived(PongResponse pongResponse)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var diff = now - pongResponse.TimeStamp;
            var total = diff + pongResponse.PingDelay;
            guiState.Log($"[{RobotName}] Ping-Pong delay: {total} ({pongResponse.PingDelay}+{diff}) ms.");
            PingButtonText.text = $"Ping:{total}";
        }



        /////////////////////////////////////////
        // GUI STUFF
        /////////////////////////////////////////
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                var contextMenu = guiState.CreateContextMenu();
                contextMenu.SetItem(eventData.position, this);
                contextMenu.SetItem(eventData.position, (ICameraLookable)this);
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                IsSelected = !IsSelected;
                OnSelectedChange(true);
            }
        }

        void OnSelectedChange(bool notify = false)
        {
            SelectedHighlightRT?.gameObject.SetActive(IsSelected);
            if (notify) guiState.OnRobotSelectionChanged(this);
        }

        public void Deselect()
        {
            IsSelected = false;
            OnSelectedChange();
            if (keyboardController != null) keyboardController.Disable();
        }

        void OnTaskAdded(int index)
        {
            var taskSpec = tasksAvailable[index];
            if (missionPlanStore.SelectedTSTGUI != null) missionPlanStore.SelectedTSTGUI.OnTaskAdded(taskSpec);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (HighlightRT != null) HighlightRT.gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (HighlightRT != null) HighlightRT.gameObject.SetActive(true);
        }

        public Transform GetWorldTarget()
        {
            Transform tf;
            if (InfoSource != InfoSource.SIM)
            {
                if (ghostTF == null) return null;
                tf = ghostTF;
            }
            else
            {
                if (simRobotBaseLinkTF == null) return null;
                tf = simRobotBaseLinkTF;
            }
            return tf;
        }


        void LateUpdate()
        {
            AddTaskButton.interactable = missionPlanStore.SelectedTSTGUI != null;
            if (keyboardController != null) keyboardController.enabled = UserInputToggle.isOn && InfoSource == InfoSource.SIM;


            if (InfoSource != InfoSource.SIM && lastHeartbeatTime > 0)
            {
                HeartRT.localScale = Vector3.Lerp(HeartRT.localScale, Vector3.one, Time.deltaTime * 10);
                isOld = Time.time - lastHeartbeatTime > OldnessTime;
                AddTaskButton.interactable = !isOld;
                TasksAvailableDropdown.interactable = !isOld;
                BGImage.color = isOld ? Color.yellow : originalColor;
            }

            if (isOld)
            {
                TSTExecInfoReceived = false;
                ghost.Freeze();
            }

            if (Time.time - lastAbortClick > 2)
            {
                AbortButton.GetComponentInChildren<TMP_Text>().text = "ABORT";
                abortCount = MaxAbortCount;
            }
        }

        public void OnDisconnected()
        {
            if (ghostTF != null) Destroy(ghostTF.gameObject);
            if (robotOverlayGO != null) Destroy(robotOverlayGO);
            Destroy(gameObject);
        }

    }
}
