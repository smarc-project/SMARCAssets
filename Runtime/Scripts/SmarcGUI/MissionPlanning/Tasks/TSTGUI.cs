using System.Collections.Generic;
using SmarcGUI.Connections;
using SmarcGUI.WorldSpace;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;



namespace SmarcGUI.MissionPlanning.Tasks
{
    public class TSTGUI : MonoBehaviour, IPointerExitHandler, IPointerEnterHandler, IPointerClickHandler, IListItem, IPathInWorld, IParamChangeListener
    {
        public TaskSpecTree tst { get; private set; }

        [Header("UI Elements")]
        public TMP_InputField DescriptionField;
        public TMP_InputField TimeoutField;
        public Toggle DrawPathToggle;
        public RectTransform HighlightRT;
        public RectTransform SelectedHighlightRT;
        public LineRenderer PathLineRenderer;

        bool isSelected = false;
        List<TaskGUI> taskGUIs = new();


        MissionPlanStore missionPlanStore;
        GUIState guiState;


        void Awake()
        {
            guiState = FindFirstObjectByType<GUIState>();
            missionPlanStore = FindFirstObjectByType<MissionPlanStore>();
            DescriptionField.onEndEdit.AddListener(OnDescriptionChanged);
            TimeoutField.onEndEdit.AddListener(OnTimeoutChanged);
            DrawPathToggle.onValueChanged.AddListener((v) => PathLineRenderer.enabled = v);
        }


        public void SetTST(TaskSpecTree tst)
        {
            this.tst = tst;

            DescriptionField.text = tst.Description;
            UpdateTasksGUI();
        }

        void OnDescriptionChanged(string desc)
        {
            if (tst == null) return;
            tst.Description = desc;
        }

        void OnTimeoutChanged(string timeout)
        {
            if (tst == null) return;
            if (float.TryParse(timeout, out float parsedTimeout))
            {
                tst.Params["timeout"] = parsedTimeout;
            }
            else
            {
                tst.Params.Remove("timeout");
            }
        }


        public void OnPointerExit(PointerEventData eventData)
        {
            HighlightRT.gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            HighlightRT.gameObject.SetActive(true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                var contextMenu = guiState.CreateContextMenu();
                contextMenu.SetItem(eventData.position, this);
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                isSelected = !isSelected;
                OnSelectionChanged();
            }
        }

        void OnSelectionChanged()
        {
            if (SelectedHighlightRT != null) SelectedHighlightRT.gameObject.SetActive(isSelected);
            missionPlanStore.OnTSTSelected(isSelected ? this : null);
            UpdateTasksGUI();
        }

        public void OnTaskAdded(TaskSpec taskSpec)
        {
            Task newTask = missionPlanStore.CreateTask(taskSpec.Name);
            if (newTask == null) return;
            tst.Children.Add(newTask);
            CreateTaskGUI(newTask);
            OnParamChanged();
        }

        void CreateTaskGUI(Task task)
        {
            // sometimes this is called before awake is called /shrug
            if (missionPlanStore == null) missionPlanStore = FindFirstObjectByType<MissionPlanStore>();
            var taskGO = Instantiate(missionPlanStore.TaskPrefab, missionPlanStore.TasksScrollContent);
            var taskGUI = taskGO.GetComponent<TaskGUI>();
            taskGUI.SetTask(task, this);
            string childCount = tst.Children.Count.ToString();
            taskGUI.SetDesc(childCount);
            taskGUIs.Add(taskGUI);
        }



        public void DeleteTask(TaskGUI taskgui)
        {
            var index = tst.Children.IndexOf(taskgui.task);
            tst.Children.RemoveAt(index);
            Destroy(taskgui.gameObject);
            taskGUIs.Remove(taskgui);
            OnParamChanged();
        }

        public void MoveTaskUp(TaskGUI taskgui)
        {
            var index = tst.Children.IndexOf(taskgui.task);
            if (index == 0) return;
            tst.Children.RemoveAt(index);
            tst.Children.Insert(index - 1, taskgui.task);
            // Swap the two TaskGUI objects
            var guiIndex = taskgui.transform.GetSiblingIndex();
            taskgui.transform.SetSiblingIndex(guiIndex - 1);

            // and then do the same thing in our taskguis list
            var guiIndexInList = taskGUIs.IndexOf(taskgui);
            taskGUIs.RemoveAt(guiIndexInList);
            taskGUIs.Insert(guiIndexInList - 1, taskgui);
            OnParamChanged();
        }

        public void MoveTaskDown(TaskGUI taskgui)
        {
            var index = tst.Children.IndexOf(taskgui.task);
            if (index == tst.Children.Count - 1) return;
            tst.Children.RemoveAt(index);
            tst.Children.Insert(index + 1, taskgui.task);
            // Swap the two TaskGUI objects
            var guiIndex = taskgui.transform.GetSiblingIndex();
            taskgui.transform.SetSiblingIndex(guiIndex + 1);

            // and then do the same thing in our taskguis list
            var guiIndexInList = taskGUIs.IndexOf(taskgui);
            taskGUIs.RemoveAt(guiIndexInList);
            taskGUIs.Insert(guiIndexInList + 1, taskgui);

            OnParamChanged();
        }

        void UpdateTasksGUI()
        {
            // maybe the first time creating this gui
            if (taskGUIs.Count == 0)
            {
                foreach (var task in tst.Children) CreateTaskGUI(task);
            }

            foreach (Transform child in missionPlanStore.TasksScrollContent)
            {
                child.gameObject.SetActive(false);
            }
            if (!isSelected) return;
            foreach (var taskGUI in taskGUIs)
            {
                taskGUI.gameObject.SetActive(true);
            }
            OnParamChanged();
        }


        public void OnDisable()
        {
            foreach (var taskGUI in taskGUIs)
            {
                taskGUI.gameObject.SetActive(false);
            }
            Deselect();
        }

        public void Deselect()
        {
            if (!isSelected) return;
            isSelected = false;
            OnSelectionChanged();
        }

        public void Select()
        {
            if (isSelected) return;
            isSelected = true;
            OnSelectionChanged();
        }

        public void OnListItemUp()
        {
            missionPlanStore.OnTSTUp(tst);
        }

        public void OnListItemDown()
        {
            missionPlanStore.OnTSTDown(tst);
        }

        public void OnListItemDelete()
        {
            missionPlanStore.OnTSTDelete(tst);
        }

        public List<Vector3> GetWorldPath()
        {
            var path = new List<Vector3>();
            foreach (var taskGUI in taskGUIs)
            {
                path.AddRange(taskGUI.GetWorldPath());
            }
            return path;
        }

        void DrawWorldPath()
        {
            var path = GetWorldPath();
            PathLineRenderer.positionCount = path.Count;
            PathLineRenderer.SetPositions(path.ToArray());
        }

        public void OnParamChanged()
        {
            DrawWorldPath();
            tst.OnTSTModified();
        }

        public void OnProjectionChanged()
        {
            foreach (var taskGUI in taskGUIs) taskGUI.OnParamChanged();
        }

    }
}