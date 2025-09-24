using UnityEngine;
using TMPro;
using System.Collections.Generic;

using Utils = DefaultNamespace.Utils;
using VehicleComponents.Sensors;
using UnityEngine.EventSystems;
using SmarcGUI.Water;
using UnityEngine.UI;
using SmarcGUI.WorldSpace;
using System;
using System.IO;



namespace SmarcGUI
{

    public class GUIState : 
    MonoBehaviour, 
    IPointerExitHandler, 
    IPointerEnterHandler
    {
        public string UUID{get; private set;}
        public bool MouseOnGUI{get; set;}
        public bool MouseDragging{get; set;}

        [Tooltip("Cursor position in normalized coordinates on the screen (0-1)")]
        public Vector2 DefaultScreenPointer => new(0.5f, 0.5f);
        float DefaultPointerX => Screen.width*DefaultScreenPointer.x;
        float DefaultPointerY => Screen.height*DefaultScreenPointer.y;
        Vector3 DefaultPointerPos => new(DefaultPointerX, DefaultPointerY, 0);


        [Header("GUI Elements")]
        public TMP_Dropdown cameraDropdown;
        public TMP_Text LogText;
        public RectTransform RobotsScrollContent;
        public Button ToggleWaterRenderButton;
        public TMP_Text ComapssText;

        [Header("Prefabs")]
        public GameObject RobotGuiPrefab;
        public GameObject ContextMenuPrefab;



        [Header("Defaults")]
        public Camera DefaultCamera;
        int defaultCameraIndex = 0;
        public float DefaultCameraLookAtMin = 1;
        public float DefaultCameraLookAtMax = 100;


        Dictionary<string, string> cameraTextToObjectPath;
        public Camera CurrentCam { get; private set; }
        public Dictionary<string, RobotGUI> RobotGuis = new();
        public RobotGUI SelectedRobotGUI {get; private set;}
        public string SelectedRobotName => SelectedRobotGUI?.RobotName;
        
        WaterRenderToggle[] waterRenderToggles;
        bool renderWaters = true;



        List<ICamChangeListener> camChangeListeners = new();
        List<IRobotSelectionChangeListener> robotSelectionChangeListeners = new();


        float mouseTwoDownTime = 0f;
        public float mouseTwoDelay = 0.15f;
        ContextMenu WorldContextMenu;


        string CameraTextFromCamera(Camera c)
        {
            var robot = Utils.FindParentWithTag(c.gameObject, "robot", false);
            if(robot == null)
            {
                if (c.transform.parent == null)
                {
                    return c.name;
                }
                return $"{c.transform.parent.name}/{c.name}";
            }
            else
            {
                return $"{robot.name}/{c.name}";
            }
        }

        void InitCameraDropdown()
        {
            cameraDropdown.onValueChanged.AddListener(OnCameraChanged);

            cameraTextToObjectPath = new Dictionary<string, string>();
            // disable all cams except the "main cam" at the start
            Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach(Camera c in cams)
            {
                // dont mess with sensor cameras
                if(c.gameObject.TryGetComponent(out Sensor s)) continue;
                // disable all cams by default. we will enable one later.
                c.enabled = false;
                // disable all audiolisteners. we got no audio. we wont enable these.
                if(c.gameObject.TryGetComponent(out AudioListener al)) al.enabled=false;
                
                string objectPath = Utils.GetGameObjectPath(c.gameObject);
                string ddText = CameraTextFromCamera(c);
                cameraTextToObjectPath.Add(ddText, objectPath);
                cameraDropdown.options.Add(new TMP_Dropdown.OptionData(){text=ddText});
            }

            for (int i = 0; i < cameraDropdown.options.Count; i++)
            {
                if (cameraDropdown.options[i].text == CameraTextFromCamera(DefaultCamera))
                {
                    defaultCameraIndex = i;
                    break;
                }
            }
            SelectDefaultCamera();
        }

        public void SelectDefaultCamera()
        {
            cameraDropdown.value = defaultCameraIndex;
            cameraDropdown.RefreshShownValue();
            OnCameraChanged(cameraDropdown.value);
        }


        public RobotGUI CreateNewRobotGUI(string robotName, InfoSource infoSource, string robotNamespace)
        {
            var robotGui = Instantiate(RobotGuiPrefab, RobotsScrollContent).GetComponent<RobotGUI>();
            robotGui.SetRobot(robotName, infoSource, robotNamespace);
            RobotGuis[robotName] = robotGui;
            Log($"Created new RobotGUI for {robotName}");
            return robotGui;
        }

        public ContextMenu CreateContextMenu()
        {
            var contextMenu = Instantiate(ContextMenuPrefab);
            return contextMenu.GetComponent<ContextMenu>();
        }

        public void RemoveRobotGUI(string robotName)
        {
            RobotGuis.Remove(robotName);
        }

        void InitRobotGuis()
        {
            GameObject[] robots = GameObject.FindGameObjectsWithTag("robot");
            foreach (var robot in robots) 
            {
                CreateNewRobotGUI(robot.name, InfoSource.SIM, "-");
            }   
        }


        public void RegisterCamChangeListener(ICamChangeListener listener)
        {
            camChangeListeners.Add(listener);
        }

        public void UnregisterCamChangeListener(ICamChangeListener listener)
        {
            camChangeListeners.Remove(listener);
        }


        public void OnCameraChanged(int camIndex)
        {
            var selection = cameraDropdown.options[camIndex];
            string objectPath = cameraTextToObjectPath[selection.text];
            GameObject selectedGO = GameObject.Find(objectPath);
            if(selectedGO == null) return;

            if(CurrentCam != null) CurrentCam.enabled = false;
            CurrentCam = selectedGO.GetComponent<Camera>();
            CurrentCam.enabled = true;

            foreach(var listener in camChangeListeners)
            {
                listener.OnCamChange(CurrentCam);
            }
        }


        public void RegisterRobotSelectionChangedListener(IRobotSelectionChangeListener listener)
        {
            if(listener == null) return;
            if(robotSelectionChangeListeners.Contains(listener)) return;
            robotSelectionChangeListeners.Add(listener);
        }

        public void UnregisterRobotSelectionChangedListener(IRobotSelectionChangeListener listener)
        {
            if(listener == null) return;
            if(!robotSelectionChangeListeners.Contains(listener)) return;
            robotSelectionChangeListeners.Remove(listener);
        }


        public void OnRobotSelectionChanged(RobotGUI robotgui)
        {
            SelectedRobotGUI = robotgui.IsSelected? robotgui : null;
            foreach(var r in RobotGuis)
            {
                if(r.Value.RobotName != robotgui.RobotName) r.Value.Deselect();
            }

            foreach(var listener in robotSelectionChangeListeners)
            {
                listener.OnRobotSelectionChange(SelectedRobotGUI);
            }
        }
        

        public void Log(string text)
        {
            string currentTime = System.DateTime.Now.ToString("HH:mm:ss");
            LogText.text = $"[{currentTime}] {text}\n{LogText.text}";
            if(LogText.text.Length > 5000)
            {
                LogText.text = LogText.text[..1000];
            }
            Debug.Log(text);
        }


        public Vector3 GetLookAtPoint()
        {
            // if the context menu for the world is open, we assume someone has r-clicked the world
            // and would rather use their pointer position than the camera position.
            // this only works if the context menu doesnt destroy itself before we get here.
            Vector3 targetPos = DefaultPointerPos;
            if(WorldContextMenu != null && WorldContextMenu.gameObject != null)
            {
                targetPos = Input.mousePosition;
            }
            Ray ray = CurrentCam.ScreenPointToRay(targetPos);
            Plane zeroPlane = new(Vector3.up, Vector3.zero);
            var dist = 10f;
            bool hitWater = false;
            if (zeroPlane.Raycast(ray, out float camToPlaneDist))
            {
                // dont want it too far...
                dist = camToPlaneDist;
                hitWater = true;
            }
            if(!hitWater)
            {
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    dist = hit.distance;
                }
            }
            dist = Mathf.Clamp(dist, DefaultCameraLookAtMin, DefaultCameraLookAtMax);
            return ray.GetPoint(dist);
        }


        void Start()
        {
            if(DefaultCamera == null) DefaultCamera = Camera.main;
            UUID = System.Guid.NewGuid().ToString();
            InitCameraDropdown();
            InitRobotGuis();
            waterRenderToggles = FindObjectsByType<WaterRenderToggle>(FindObjectsSortMode.None);
            ToggleWaterRenderButton.onClick.AddListener(() => {
                foreach(var toggle in waterRenderToggles)
                {
                    renderWaters = !renderWaters;
                    toggle.ToggleWaterRender(renderWaters);
                }
            });
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            MouseOnGUI = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            MouseOnGUI = true;
        }

        void UpdateCompass()
        {
            // Update the compass text WRT the current camera
            if(CurrentCam == null) ComapssText.text = "NO CAM";
            else
            {
                Vector3 camForward = CurrentCam.transform.forward;
                // check if camera is looking straight up or down
                // if camForward.y is 0, then the camera is looking straight up or down
                if(Mathf.Abs(camForward.x) < 0.1f && Mathf.Abs(camForward.z) < 0.1f)
                {
                    // camera looking straigh up or down...
                    ComapssText.text = "UP/DOWN";
                    return;
                }
                // calculate the angle the camera is looking towards
                // and turn it into a compass direction
                // project forward onto the xz plane
                camForward.y = 0;
                float angle = Vector3.SignedAngle(Vector3.forward, camForward, Vector3.up);
                // convert to compass direction
                string[] compassDirections = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                int index = Mathf.RoundToInt((angle + 360) / 45) % 8;
                angle = (angle + 360) % 360; // normalize angle to [0, 360)
                ComapssText.text = $"{angle:F1}° ({compassDirections[index]})";
            }
        }

        void LateUpdate()
        {
            UpdateCompass();

            // cant use the input system mouse events because we are after mouse-not-over-gui usage of the mouse!
            // so we have to use the old input system for this.
            if(!MouseOnGUI && Input.GetMouseButtonUp(1))
            {
                if(CurrentCam != null && CurrentCam.TryGetComponent(out FlyCamera flyCam))
                {
                    flyCam.EnableMouseLook(false);
                }
                if(mouseTwoDownTime < mouseTwoDelay)
                {
                    // normal right click.
                    mouseTwoDownTime = 0f;
                    // create a context menu at the mouse position.
                    WorldContextMenu = CreateContextMenu();
                    WorldContextMenu.SetItem(Input.mousePosition);
                }
            }
            
            if(!MouseOnGUI && Input.GetMouseButton(1))
            {
                mouseTwoDownTime += Time.deltaTime;
                if(mouseTwoDownTime > mouseTwoDelay)
                {
                    // right click and hold.
                    if(CurrentCam != null && CurrentCam.TryGetComponent(out FlyCamera flyCam))
                    {
                        // enable the camera mouse look.
                        flyCam.EnableMouseLook(true);
                    }
                }
            }
            else mouseTwoDownTime = 0f;
        }

        public static string GetStoragePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SMaRCUnity");
        }

    }
}