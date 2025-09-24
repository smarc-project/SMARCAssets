using SmarcGUI.MissionPlanning;
using SmarcGUI.WorldSpace;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace SmarcGUI
{
    public class ContextMenu : MonoBehaviour, IPointerExitHandler
    {
        RectTransform rt;
        Canvas canvas;
        GUIState guiState;
        MissionPlanStore missionPlanStore;

        [Tooltip("Generic ListItem")]
        IListItem listItem;
        public GameObject ListItemSection;
        public Button DeleteButton;
        public Button MoveUpButton;
        public Button MoveDownButton;


        [Tooltip("Robot")]
        public GameObject RobotSection;
        RobotGUI robotItem;
        public Button PingButton;


        [Tooltip("WorldObject")]
        public GameObject WorldObjectSection;
        ICameraLookable cameraLookableItem;
        public Button LookAtButton;
        public Button FollowButton;

        [Tooltip("World")]
        public GameObject WorldSection;
        public Button AddTaskButton;


        void Awake()
        {
            rt = GetComponent<RectTransform>();
            canvas = GameObject.Find("Canvas-Over").GetComponent<Canvas>();
            guiState = FindFirstObjectByType<GUIState>();
            missionPlanStore = FindFirstObjectByType<MissionPlanStore>();
            // Disable all sections by default
            foreach(Transform child in transform) child.gameObject.SetActive(false);
        }

        protected void SetOnTopResize(Vector2 position)
        {
            rt.SetParent(canvas.transform, false);
            rt.position = position;
            rt.SetAsLastSibling();
            
            var selfHeight = 10f;
            foreach(Transform child in transform)
                if(child.gameObject.activeSelf)
                    selfHeight += child.GetComponent<RectTransform>().sizeDelta.y;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, selfHeight);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Destroy(gameObject);
        }


        // Generic ListItem
        public void SetItem(Vector2 position, IListItem item)
        {
            DeleteButton.onClick.AddListener(OnItemDelete);
            MoveUpButton.onClick.AddListener(OnItemUp);
            MoveDownButton.onClick.AddListener(OnItemDown);
            ListItemSection.SetActive(true);
            listItem = item;
            SetOnTopResize(position);
        }
        

        void OnItemDelete()
        {
            listItem.OnListItemDelete();
            Destroy(gameObject);
        }

        void OnItemUp()
        {
            listItem.OnListItemUp();
            Destroy(gameObject);
        }

        void OnItemDown()
        {
            listItem.OnListItemDown();
            Destroy(gameObject);
        }

        // Robot
        public void SetItem(Vector2 position, RobotGUI item)
        {
            PingButton.onClick.AddListener(OnPing);
            RobotSection.SetActive(true);
            robotItem = item;
            SetOnTopResize(position);
        }

        void OnPing()
        {
            robotItem.SendPing();
            Destroy(gameObject);
        }


        
        // WorldObject
        public void SetItem(Vector2 position, ICameraLookable item)
        {
            LookAtButton.onClick.AddListener(OnLookAt);
            FollowButton.onClick.AddListener(OnFollow);
            WorldObjectSection.SetActive(true);
            cameraLookableItem = item;
            SetOnTopResize(position);
        }

        void OnFollow()
        {
            var tf = cameraLookableItem.GetWorldTarget();
            guiState.SelectDefaultCamera();
            var cam = guiState.CurrentCam;
            cam.GetComponent<SmoothFollow>().target = tf;
            Destroy(gameObject);
        }

        void OnLookAt()
        {
            var tf = cameraLookableItem.GetWorldTarget();
            // guiState.SelectDefaultCamera();
            var cam = guiState.CurrentCam;
            cam.transform.position = new Vector3(tf.position.x, cam.transform.position.y, tf.position.z);
            if(cam.TryGetComponent(out SmoothFollow smooth))
            {
                smooth.target = null;
                cam.transform.LookAt(tf);
            }
            Destroy(gameObject);
        }
        

        // Empty world
        public void SetItem(Vector2 position)
        {
            AddTaskButton.onClick.AddListener(OnAddTask);
            WorldSection.SetActive(missionPlanStore.SelectedTSTGUI != null);
            SetOnTopResize(position);
        }

        void OnAddTask()
        {
            missionPlanStore.AddNewTask();
            Destroy(gameObject);
        }

    }
}
