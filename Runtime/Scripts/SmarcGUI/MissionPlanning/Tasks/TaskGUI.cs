using UnityEngine;
using TMPro;
using System.Collections.Generic;

using UnityEngine.EventSystems;
using System.Linq;
using SmarcGUI.WorldSpace;
using SmarcGUI.MissionPlanning.Params;
using UnityEngine.UI;



namespace SmarcGUI.MissionPlanning.Tasks
{
    public class TaskGUI : 
    MonoBehaviour, 
    IHeightUpdatable, 
    IRobotSelectionChangeListener, 
    IPointerClickHandler, 
    IPointerExitHandler, 
    IPointerEnterHandler, 
    IListItem, 
    IPathInWorld, 
    IParamChangeListener,
    ICameraLookable
    {
        public float BottomPadding = 5;
        public Task task;

        [Header("UI Elements")]
        public GameObject Params;
        public TMP_InputField DescriptionField;
        public TMP_Text TaskName;
        public RectTransform HighlightRT;
        public GameObject SelectedHighlightGO;
        public Button RunButton;
        public RectTransform WarningRT;
        bool isSelected;

        [Header("Prefabs")]
        public GameObject PointMarkerPrefab;



        [Header("Worldspace")]
        public string WorldMarkersCollectionName = "WorldMarkers";
        Transform WorldMarkersCollection;
        List<PointMarker> pointmarkers = new();


        MissionPlanStore missionPlanStore;
        GUIState guiState;
        public TSTGUI tstGUI{ get; private set; }
        RectTransform rt;
        float baseHeight;
        Image RunButtonImage;
        Color RunButtonOriginalColor;
        TMP_Text RunButtonText;


        void Awake()
        {
            rt = GetComponent<RectTransform>();
            baseHeight = rt.sizeDelta.y;
            missionPlanStore = FindFirstObjectByType<MissionPlanStore>();
            guiState = FindFirstObjectByType<GUIState>();
            DescriptionField.onEndEdit.AddListener(SetDesc);
            RunButton.onClick.AddListener(OnRunTask);
            RunButtonImage = RunButton.GetComponent<Image>();
            RunButtonText = RunButton.GetComponentInChildren<TMP_Text>();
            RunButtonOriginalColor = RunButtonImage.color;

            WorldMarkersCollection = GameObject.Find(WorldMarkersCollectionName).transform;

            OnRobotSelectionChange(guiState.SelectedRobotGUI);
        }
        
        void OnRunTask()
        {
            var robotgui = guiState.SelectedRobotGUI;
            if(robotgui == null) return;
            robotgui.SendStartTaskCommand(task);
        }

        public void SetDesc(string desc)
        {
            if(task == null) return;
            task.Description = desc;
            DescriptionField.text = desc;
            foreach(var p in pointmarkers) p.SetNameDesc(task.Name, task.Description);
        }

        public PointMarker AddPointMarker(ParamGUI paramgui)
        {
            if(paramgui is IParamHasXZ paramXZ)
            {
                var markerGO = Instantiate(PointMarkerPrefab, WorldMarkersCollection);
                PointMarker pointmarker = markerGO.GetComponent<PointMarker>();
                pointmarker.SetXZParam(paramXZ);
                pointmarker.SetNameDesc(task.Name, task.Description);

                if(paramgui is IParamHasY paramY) pointmarker.SetYParam(paramY);
                if(paramgui is IParamHasHeading paramH) pointmarker.SetHeadingParam(paramH);
                if(paramgui is IParamHasOrientation paramO) pointmarker.SetOrientationParam(paramO);
                if(paramgui is IParamHasTolerance paramT) pointmarker.SetToleranceParam(paramT);
                pointmarker.OnParamChanged();
                pointmarkers.Add(pointmarker);
                return pointmarker;
            }
            return null;
        }

        public void RemovePointMarker(ParamGUI paramgui)
        {
            foreach(var p in pointmarkers)
            {
                if(p == paramgui) Destroy(p.gameObject);
            }
        }

        public void SetTask(Task task, TSTGUI tstGUI)
        {
            this.task = task;
            this.tstGUI = tstGUI;
            TaskName.text = task.Name;
            DescriptionField.text = task.Description;
            
            guiState.RegisterRobotSelectionChangedListener(this);


            // we create world-markers for each parameter that actually has a position
            // if no parameter in the task has a position, it can not be visualized in the world
            // if a parameter has its horizontal position and vertical position defined by separate
            // parameters, then we first create the horizontal point, then _add_ the info from
            // following parameters onto the same point, until a new horizontal point containing parameter
            // is found. This allows tasks with split poses (like latlon + depth + oritentaiton as separate params)
            // Meaning, a task can split the 6DOF pose into 2+1+1+1+1 if it wants to...
            for(int i=0; i<task.Params.Count; i++)
            {
                var paramgui = InstantiateParamGui(Params.transform, task.Params, task.Params.Keys.ElementAt(i));
                if(paramgui is ListParamGUI listParamGUI)
                {
                    listParamGUI.ReArrangeForList();
                    listParamGUI.SetListLabels();
                    foreach (var p in listParamGUI.ParamGUIs) AddPointMarker(p);
                }
                else AddPointMarker(paramgui);
            }
            UpdateHeight();
        }

        ParamGUI InstantiateParamGui(Transform parent, Dictionary<string, object> taskParams, string paramKey)
        {
            if(missionPlanStore == null) missionPlanStore = FindFirstObjectByType<MissionPlanStore>();
            GameObject paramPrefab = missionPlanStore.GetParamPrefab(taskParams[paramKey]);
            GameObject paramGO = Instantiate(paramPrefab, parent);
            var paramgui = paramGO.GetComponent<ParamGUI>();
            paramgui.SetParam(taskParams, paramKey, this);
            return paramgui;
        }


        public void UpdateHeight()
        {
            float newHeight = baseHeight;
            if(Params.activeSelf)
            {
                float paramsHeight = 0;
                foreach(Transform child in Params.transform)
                {
                    var paramRT = child.GetComponent<RectTransform>();
                    paramsHeight += paramRT.sizeDelta.y;
                }
                newHeight += paramsHeight + BottomPadding;
            }
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, newHeight);
        }


        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button == PointerEventData.InputButton.Right)
            {
                var contextMenu = guiState.CreateContextMenu();
                contextMenu.SetItem(eventData.position, (IListItem)this);
                contextMenu.SetItem(eventData.position, (ICameraLookable)this);
            }

            if(eventData.button == PointerEventData.InputButton.Left)
            {
                isSelected = !isSelected;
                if(SelectedHighlightGO != null) SelectedHighlightGO.SetActive(isSelected);
                foreach(var p in pointmarkers) p.Selected(isSelected);
            }

        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HighlightRT.gameObject.SetActive(false);
            foreach(var p in pointmarkers) p.Highlight(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            HighlightRT.gameObject.SetActive(true);
            foreach(var p in pointmarkers) p.Highlight(true);
        }

        public void OnRobotSelectionChange(RobotGUI SelectedRobotGUI)
        {
            RunButton.interactable = SelectedRobotGUI != null;
            if(SelectedRobotGUI == null || SelectedRobotGUI.TasksAvailableNames == null)
            {
                WarningRT.gameObject.SetActive(false);
                RunButtonImage.color = Color.gray;
                RunButtonText.text = SelectedRobotGUI == null ? "NoRobot" : "NoTasks";
            }
            else
            {
                // warning highlight if the selected robot does not have this task available
                if(SelectedRobotGUI.InfoSource == InfoSource.SIM) WarningRT.gameObject.SetActive(false);
                else if (!SelectedRobotGUI.TasksAvailableNames.Contains(task.Name)) WarningRT.gameObject.SetActive(true);

                // make the RUN button green if it is already running this task
                // use the task uuid to check this, since many tasks of the same type can be running
                // 
                if(SelectedRobotGUI.TasksExecutingUuids.Contains(task.TaskUuid))
                {
                    RunButtonImage.color = Color.green;
                    RunButton.interactable = false;
                    RunButtonText.text = "Running";
                }
                else
                {
                    RunButtonImage.color = RunButtonOriginalColor;
                    RunButtonText.text = "Run";
                }
            } 
        }


        void OnEnable()
        {
            foreach (Transform child in Params.transform)
            {
                child.gameObject.SetActive(true);
            }
            UpdateHeight();
            guiState.RegisterRobotSelectionChangedListener(this);
            foreach(var p in pointmarkers) p.gameObject.SetActive(true);
        }

        void OnDisable()
        {
            foreach (Transform child in Params.transform)
            {
                child.gameObject.SetActive(false);
            }
            guiState.UnregisterRobotSelectionChangedListener(this);
            foreach(var p in pointmarkers) p.gameObject.SetActive(false);

        }

        public void OnListItemUp()
        {
            tstGUI.MoveTaskUp(this);
        }

        public void OnListItemDown()
        {
            tstGUI.MoveTaskDown(this);
        }

        public void OnListItemDelete()
        {
            guiState.UnregisterRobotSelectionChangedListener(this);
            tstGUI.DeleteTask(this);
            foreach(var p in pointmarkers) Destroy(p.gameObject);
        }

        public List<Vector3> GetWorldPath()
        {
            var path = new List<Vector3>();
            foreach(Transform paramguiTF in Params.transform)
            {
                if(!paramguiTF.TryGetComponent<ParamGUI>(out var paramgui)) continue;
                if(paramgui is IPathInWorld pathInWorld)
                {
                    path.AddRange(pathInWorld.GetWorldPath());
                }
                else if(paramgui is IParamHasXZ paramXZ)
                {
                    var (x,z) = paramXZ.GetXZ();
                    if(paramgui is IParamHasY paramY)
                    {
                        var y = paramY.GetY();
                        path.Add(new Vector3(x, y, z));
                    }
                    else
                    {
                        path.Add(new Vector3(x, 0, z));
                    }
                }
            }
            return path;
        }

        public void OnParamChanged()
        {
            foreach(var p in pointmarkers) p.OnParamChanged();
            task.OnTaskModified();
            tstGUI.OnParamChanged();
        }

        public Transform GetWorldTarget()
        {
            if(pointmarkers.Count > 1) return pointmarkers[0].transform;
            else return null;
        }
    }
}