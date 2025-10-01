using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Robotics.ROSTCPConnector;
using ROS.Publishers;
using ROS.Core;
using System.IO;
using System.Collections.Generic;

namespace SmarcGUI.Connections
{
    public class ROSClientGUI : MonoBehaviour
    {
        [Header("Connection Settings")]
        [Tooltip("If true, the default settings below will override any saved settings file.")]
        public bool OverrideSettingsFile = false;
        public string DefaultServerAddress = "localhost";
        public int DefaultPort = 10000;
        public bool ConnectOnStart = false;

        [Header("UI Elements")]
        public TMP_InputField ServerAddressInput;
        public TMP_InputField PortInput;
        public Button ConnectButton;
        public TMP_Text ConnectButtonText;
        public Toggle PublishControllerToRosToggle;

        ROSConnection rosCon;
        public bool IsConnected = false;

        string ServerAddress => ServerAddressInput.text;
        int ServerPort => int.Parse(PortInput.text);

        ROSBehaviour[] rosBehaviours;

        Joy_Pub joyPub;

        GUIState guiState;

        void Awake()
        {
            var joyPubs = FindObjectsByType<Joy_Pub>(FindObjectsSortMode.None);
            if(joyPubs.Length >= 1)
            {   
                if(joyPubs.Length > 1)
                {
                    Debug.LogWarning("Multiple Joy_Pub components found in the scene. Using the first one and disabling the rest.");
                    for(int i = 1; i < joyPubs.Length; i++)
                    {
                        joyPubs[i].enabled = false;
                    }
                }
                joyPub = joyPubs[0];
                joyPub.enabled = false;
            }

            rosBehaviours = FindObjectsByType<ROSBehaviour>(FindObjectsSortMode.None);

            guiState = FindFirstObjectByType<GUIState>();
        }

        void Start()
        {
            rosCon = ROSConnection.GetOrCreateInstance();

            bool connectNow = false;

            string settingsStoragePath = Path.Combine(GUIState.GetStoragePath(), "Settings");
            Directory.CreateDirectory(settingsStoragePath);
            string settingsFile = Path.Combine(settingsStoragePath, "ROSSettings.yaml");
            if (File.Exists(settingsFile))
            {
                var settings = File.ReadAllText(settingsFile);
                var deserializer = new YamlDotNet.Serialization.Deserializer();
                var settingsDict = deserializer.Deserialize<Dictionary<string, string>>(settings);
                if (settingsDict.ContainsKey("ROS_TCP_ConnectorIP")) ServerAddressInput.text = settingsDict["ROS_TCP_ConnectorIP"];
                if (settingsDict.ContainsKey("ROS_TCP_ConnectorPort")) PortInput.text = settingsDict["ROS_TCP_ConnectorPort"];
                if (settingsDict.ContainsKey("ConnectOnStart")) connectNow = settingsDict["ConnectOnStart"].ToLower() == "true";
            }
            else
            {
                var settingsDict = new Dictionary<string, string>
                {
                    { "ROS_TCP_ConnectorIP", rosCon.RosIPAddress.ToString() },
                    { "ROS_TCP_ConnectorPort", rosCon.RosPort.ToString() },
                    { "ConnectOnStart", "false" }
                };
                var serializer = new YamlDotNet.Serialization.Serializer();
                var settingsYaml = serializer.Serialize(settingsDict);
                File.WriteAllText(settingsFile, settingsYaml);
                guiState.Log($"No ROS settings file found. Created default settings file at {settingsFile}");
                ServerAddressInput.text = rosCon.RosIPAddress.ToString();
                PortInput.text = rosCon.RosPort.ToString();
                rosCon.ConnectOnStart = false;
            }

            if (OverrideSettingsFile)
            {
                ServerAddressInput.text = DefaultServerAddress;
                PortInput.text = DefaultPort.ToString();
                connectNow = ConnectOnStart;
            }

            ConnectButton.onClick.AddListener(ToggleConnection);
            if (joyPub != null) PublishControllerToRosToggle.onValueChanged.AddListener(value => joyPub.enabled = value);
            else PublishControllerToRosToggle.interactable = false;
            rosCon.ShowHud = false;
            
            if (connectNow) ToggleConnection();
        }

        void ConnectionInputsInteractable(bool interactable)
        {
            ServerAddressInput.interactable = interactable;
            PortInput.interactable = interactable;
            PublishControllerToRosToggle.interactable = interactable;
        }

        void ToggleConnection()
        {
            if(IsConnected)
            {
                Debug.Log($"Disconnecting from ROS-TCP bridge at: {ServerAddress}:{ServerPort}");
                rosCon.Disconnect();
                IsConnected = false;
                ConnectButtonText.text = "Connect";
            }
            else
            {
                Debug.Log($"Connecting to ROS-TCP bridge at: {ServerAddress}:{ServerPort}");
                rosCon.Connect(ServerAddress, ServerPort);
                IsConnected = true;
                ConnectButtonText.text = "Disconnect";
            }

            ConnectionInputsInteractable(!IsConnected);
            foreach(var b in rosBehaviours)
            {
                b.enabled = IsConnected;
            }

        }

    }
}