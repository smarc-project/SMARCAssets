using System;
using System.Threading;
using SystemTask = System.Threading.Tasks.Task; // to diff from SmarcGUI.MissionPlanning.Tasks.Task

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using SmarcGUI.MissionPlanning.Params;
using System.Security.Authentication;
using System.IO;
using YamlDotNet;

namespace SmarcGUI.Connections
{
    public enum WaspUnitType
    {
        air,
        ground,
        surface,
        subsurface
    }

    public enum WaspLevels
    {
        sensor,
        direct_execution,
        tst_execution,
        delegation
    }


    public class MQTTClientGUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TMP_InputField ServerAddressInput;
        public TMP_InputField PortInput;
        public TMP_InputField ContextInput;
        public Toggle SubToSimToggle;
        public Toggle SubToRealToggle;
        public Toggle TLSToggle;
        public TMP_InputField UserNameInput;
        public TMP_InputField PasswordInput;

        public Button ConnectButton;
        public TMP_Text ConnectButtonText;

        // mostly a wrapper for: https://github.com/dotnet/MQTTnet/blob/release/4.x.x/Samples/Client/Client_Connection_Samples.cs
        // Notice we use the 4.x branch because dotnet of unity (:

        IMqttClient mqttClient;
        GUIState guiState;

        public string Context => ContextInput.text;

        string ServerAddress => ServerAddressInput.text;
        int ServerPort => int.Parse(PortInput.text);

        MQTTPublisher[] publishers;

        Queue<Tuple<string, string>> mqttInbox = new();
        HashSet<string> subscribedTopics = new();

        void Awake()
        {
            guiState = FindFirstObjectByType<GUIState>();
            ContextInput.text = "smarcsim";
            publishers = FindObjectsByType<MQTTPublisher>(FindObjectsSortMode.None);
        }

        void Start()
        {
            bool connectNow = false;

            string settingsStoragePath = Path.Combine(GUIState.GetStoragePath(), "Settings");
            Directory.CreateDirectory(settingsStoragePath);
            string settingsFile = Path.Combine(settingsStoragePath, "MQTTSettings.yaml");
            if (File.Exists(settingsFile))
            {
                var settings = File.ReadAllText(settingsFile);
                var deserializer = new YamlDotNet.Serialization.Deserializer();
                var settingsDict = deserializer.Deserialize<Dictionary<string, string>>(settings);
                if (settingsDict.ContainsKey("BrokerAddress")) ServerAddressInput.text = settingsDict["BrokerAddress"];
                if (settingsDict.ContainsKey("BrokerPort")) PortInput.text = settingsDict["BrokerPort"];
                if (settingsDict.ContainsKey("Context")) ContextInput.text = settingsDict["Context"];
                if (settingsDict.ContainsKey("SubToReal")) SubToRealToggle.isOn = bool.Parse(settingsDict["SubToReal"]);
                if (settingsDict.ContainsKey("SubToSim")) SubToSimToggle.isOn = bool.Parse(settingsDict["SubToSim"]);
                if (settingsDict.ContainsKey("TLS")) TLSToggle.isOn = bool.Parse(settingsDict["TLS"]);
                if (settingsDict.ContainsKey("Username")) UserNameInput.text = settingsDict["Username"];
                if (settingsDict.ContainsKey("Password")) PasswordInput.text = settingsDict["Password"];
                if (settingsDict.ContainsKey("ConnectOnStart")) connectNow = settingsDict["ConnectOnStart"].ToLower() == "true";

            }
            else
            {
                // Default settings if no settings file exists
                var settingsDict = new Dictionary<string, string>
                {
                    { "BrokerAddress", "localhost" },
                    { "BrokerPort", "1889" },
                    { "Context", "smarcsim" },
                    { "SubToReal", "true" },
                    { "SubToSim", "true" },
                    { "TLS", "false" },
                    { "Username", "" },
                    { "Password", "" },
                    { "ConnectOnStart", "false" }
                };
                var serializer = new YamlDotNet.Serialization.Serializer();
                var settingsYaml = serializer.Serialize(settingsDict);
                File.WriteAllText(settingsFile, settingsYaml);
                guiState.Log($"No MQTT settings file found. Created default settings file at {settingsFile}");
                ServerAddressInput.text = "localhost";
                PortInput.text = "1889";
                ContextInput.text = "smarcsim";
                SubToRealToggle.isOn = true;
                SubToSimToggle.isOn = true;
                TLSToggle.isOn = false;
                UserNameInput.text = "";
                PasswordInput.text = "";
            }

            ConnectButton.onClick.AddListener(ToggleConnection);
            ConnectionInputsInteractable(true);

            if (connectNow) ToggleConnection();
        }

        void ConnectionInputsInteractable(bool interactable)
        {
            ServerAddressInput.interactable = interactable;
            PortInput.interactable = interactable;
            ContextInput.interactable = interactable;
            SubToRealToggle.interactable = interactable;
            SubToSimToggle.interactable = interactable;
            TLSToggle.interactable = interactable;
            UserNameInput.interactable = interactable;
            PasswordInput.interactable = interactable;
        }


        SystemTask OnMsgReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            // we cant do anything in this thread, because all the things we want to do,
            // are tied to unity objects in multiple ways. and unity objects are not thread safe.
            // so we just enqueue the message and handle it in the main thread in Update()
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString();
            mqttInbox.Enqueue(new Tuple<string, string>(topic, payload));
            return SystemTask.CompletedTask;
        }

        void OnConnectionMade()
        {
            if(SubToRealToggle.isOn) SubToHeartbeats("real");
            if(SubToSimToggle.isOn) SubToHeartbeats("simulation");
            foreach(var publisher in publishers)
            {
                publisher.StartPublishing();
            }
            ConnectButtonText.text = "Disconnect";
        }

        void OnConnectionLost()
        {
            foreach(var publisher in publishers)
            {
                publisher.StopPublishing();
            }
            subscribedTopics.Clear();

            List<string> toRemove = new();
            foreach(var robotgui in guiState.RobotGuis.Values)
            {
                if(robotgui.InfoSource == InfoSource.MQTT)
                {
                    toRemove.Add(robotgui.RobotName);
                    robotgui.OnDisconnected();
                }
            }
            foreach(var robotName in toRemove)
            {
                guiState.RemoveRobotGUI(robotName);
            }

            ConnectButtonText.text = "Connect";
        }

        void ToggleConnection()
        {
            if(mqttClient is null || !mqttClient.IsConnected)
            {
                Debug.Log($"Connecting to MQTT broker at: {ServerAddress}:{ServerPort}");
                ConnectToBroker();
            }
            else
            {
                Debug.Log($"Disconnecting from MQTT broker at: {ServerAddress}:{ServerPort}");
                DisconnectFromBroker();
            }
        }


        async void ConnectToBroker()
        {
            var mqttFactory = new MqttFactory();
            mqttClient = mqttFactory.CreateMqttClient();

            var mqttClientOptionsUnbuilt = new MqttClientOptionsBuilder().WithTcpServer(host: ServerAddress, port: ServerPort);

            if(!string.IsNullOrEmpty(UserNameInput.text) && !string.IsNullOrEmpty(PasswordInput.text))
            {
                mqttClientOptionsUnbuilt = mqttClientOptionsUnbuilt.WithCredentials(UserNameInput.text, PasswordInput.text);
            }

            if(TLSToggle.isOn)
            {
                mqttClientOptionsUnbuilt = mqttClientOptionsUnbuilt.WithTlsOptions(
                    o =>
                    {
                        o.WithCertificateValidationHandler(
                            eventArgs =>
                            {
                                Debug.Log(eventArgs.Certificate.Subject);
                                Debug.Log(eventArgs.Certificate.GetExpirationDateString());
                                Debug.Log(eventArgs.Chain.ChainPolicy.RevocationMode);
                                Debug.Log(eventArgs.Chain.ChainStatus);
                                Debug.Log(eventArgs.SslPolicyErrors);
                                return true;
                            }
                        );

                        // The default value is determined by the OS. Set manually to force version.
                        o.WithSslProtocols(SslProtocols.Tls12);
                    });
            }

            var mqttClientOptions = mqttClientOptionsUnbuilt.Build();

            mqttClient.ApplicationMessageReceivedAsync += OnMsgReceived;

            guiState.Log($"Connecting to {ServerAddress}:{ServerPort} ...");
            MqttClientConnectResult response = null;
            try
            {
                ConnectionInputsInteractable(false);
                response = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
            }
            catch (MqttCommunicationTimedOutException)
            {
                ConnectionInputsInteractable(true);
                guiState.Log($"Timeout while trying to connect to {ServerAddress}:{ServerPort}");
                return;
            }
            catch (MqttCommunicationException)
            {
                ConnectionInputsInteractable(true);
                guiState.Log($"Communication exception while trying to connect to {ServerAddress}:{ServerPort}");
                return;
            }
            catch (OperationCanceledException)
            {
                ConnectionInputsInteractable(true);
                guiState.Log($"Connection to {ServerAddress}:{ServerPort} was canceled");
                return;
            }

            if(response is null || response.ResultCode != MqttClientConnectResultCode.Success)
            {
                ConnectionInputsInteractable(true);
                guiState.Log($"Failed to connect to {ServerAddress}:{ServerPort}, result code == {response.ResultCode}");
                return;
            }
            guiState.Log($"Connected to broker on {ServerAddress}:{ServerPort}!");

            OnConnectionMade();
        }

        async void DisconnectFromBroker()
        {
            var mqttFactory = new MqttFactory();
            var mqttClientDisconnectOptions = mqttFactory.CreateClientDisconnectOptionsBuilder().Build();
            try
            {
                await mqttClient.DisconnectAsync(mqttClientDisconnectOptions, CancellationToken.None);
            }
            catch (MqttClientNotConnectedException)
            {
                guiState.Log($"Not connected to broker on {ServerAddress}:{ServerPort}!");
                ConnectionInputsInteractable(true);
                return;
            }
            ConnectionInputsInteractable(true);
            guiState.Log($"Disconnected from broker on {ServerAddress}:{ServerPort}!");
            OnConnectionLost();
        }

    
        public async void Publish(string topic, string payload)
        {
            if(mqttClient is null || !mqttClient.IsConnected) return;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();


            try
            {
                await mqttClient.PublishAsync(message, CancellationToken.None);
            }
            catch (MqttCommunicationTimedOutException)
            {
                guiState.Log($"Timeout while trying to publish message to {ServerAddress}:{ServerPort}");
                return;
            }
            catch (OperationCanceledException)
            {
                guiState.Log($"Publishing message to {ServerAddress}:{ServerPort} was canceled");
                return;
            }
        }

        public async void SubToTopic(string topic)
        {
            if(subscribedTopics.Contains(topic)) return;
            Debug.Log($"Subscribing to topic: {topic} ...");

            var mqttFactory = new MqttFactory();
            var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(topic)
                .Build();

            await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);

            subscribedTopics.Add(topic);
            Debug.Log($"MQTT client subscribed to topic: {topic}");
        }

        void SubToHeartbeats(string realism)
        {
            var topic = $"{Context}/unit/+/{realism}/+/heartbeat";
            SubToTopic(topic);
        }

        void HandleMQTTMsg(Tuple<string, string> topicPayload)
        {
            if(topicPayload == null) return;
            if(topicPayload.Item1 == null || topicPayload.Item2 == null) return;
            
            var topic = topicPayload.Item1;
            var payload = topicPayload.Item2;

            try
            {
                HandleWaspMQTTMsg(topic, payload);
            }
            catch(Exception e)
            {
                guiState.Log($"Error while handling MQTT message on topic: {topic}");
                guiState.Log(e.Message);
            }

        }


        void HandleWaspMQTTMsg(string topic, string payload)
        {
            // wara stuff is formatted like: smarc/unit/subsurface/simulation/sam1/heartbeat
            // {context}/unit/{air,ground,surface,subsurface}/{real,simulation,playback}/{agentName}/{topic}
            var topicParts = topic.Split('/');
            var context = topicParts[0];
            var domain = topicParts[2];
            var realism = topicParts[3];
            var agentName = topicParts[4];
            var messageType = topicParts[5];
            
            if(!guiState.RobotGuis.ContainsKey(agentName))
            {
                string robotNamespace = $"{context}/unit/{domain}/{realism}/{agentName}/";
                guiState.CreateNewRobotGUI(agentName, InfoSource.MQTT, robotNamespace);
            }
            
            var robotgui = guiState.RobotGuis[agentName];
            switch(messageType)
            {
                case "heartbeat":
                    WaspHeartbeatMsg heartbeat = new(payload);
                    robotgui.OnHeartbeatReceived(heartbeat);
                    break;
                case "sensor_info":
                    WaspSensorInfoMsg sensorInfo = new(payload);
                    robotgui.OnSensorInfoReceived(sensorInfo);
                    break;
                case "direct_execution_info":
                    WaspDirectExecutionInfoMsg directExecutionInfo = new(payload);
                    robotgui.OnDirectExecutionInfoReceived(directExecutionInfo);
                    break;
                case "tst_execution_info":
                    WaspTSTExecutionInfoMsg tstExecutionInfo = new(payload);
                    robotgui.OnTSTExecutionInfoReceived(tstExecutionInfo);
                    break;
                case "exec":
                    var exec_type = topicParts[6];
                    switch(exec_type)
                    {
                        case "command":
                            BaseCommand cmd = new(payload);
                            if(cmd.Sender == "UnityGUI") return; // ignore self commands lol
                            switch(cmd.Command)
                            {
                                case "ping":
                                    PingCommand pingCmd = new(payload);
                                    robotgui.OnPingCmdReceived(pingCmd);
                                    break;
                                default:
                                    guiState.Log($"{topic}\n{payload}");
                                    break;
                            }
                            break;
                        case "response":
                            BaseResponse response = new(payload);
                            switch(response.Response)
                            {
                                case "pong":
                                    PongResponse pong = new(payload);
                                    robotgui.OnPongResponseReceived(pong);
                                    break;
                                default:
                                    guiState.Log($"{topic}\n{payload}");
                                    break;
                            }
                            break;  
                        case "feedback":
                            guiState.Log($"{topic}\n{payload}");
                            break;
                        default:
                            guiState.Log($"{topic}\n{payload}");
                            break;
                    }
                    break;
                case "sensor":
                    // there could be _many_ different kinds of sensors,
                    // some of these, we will have specific ways to visualize, like the basics
                    // of position, heading, course, speed
                    // others, we will have some generic ways... eventually.
                    var sensor_type = topicParts[6];
                    switch(sensor_type)
                    {
                        case "position":
                            GeoPoint pos = new(payload);
                            robotgui.OnPositionReceived(pos);
                            break;
                        case "heading":
                            float heading = float.Parse(payload);
                            robotgui.OnHeadingReceived(heading);
                            break;
                        case "course":
                            float course = float.Parse(payload);
                            robotgui.OnCourseReceived(course);
                            break;
                        case "speed":
                            float velocity = float.Parse(payload);
                            robotgui.OnSpeedReceived(velocity);
                            break;
                        case "pitch":
                            float pitch = float.Parse(payload);
                            robotgui.OnPitchReceived(pitch);
                            break;
                        case "roll":
                            float roll = float.Parse(payload);
                            robotgui.OnRollReceived(roll);
                            break;
                        case "depth":
                            //TODO
                            break;
                        case "executing_tasks":
                            //TODO
                            break;
                        default:
                            guiState.Log($"{topic}\n{payload}");
                            break;
                    }
                    break;
                default:
                    guiState.Log($"Received uhandled message on MQTT topic: {topic}. You should add this into MQTTClientGUI.HandleMQTTMsg!");
                    break;
            }
        }

        void Update()
        {
            if(mqttInbox.Count == 0) return;
            while(mqttInbox.Count > 0) HandleMQTTMsg(mqttInbox.Dequeue());
        }
        

        


    }
        
}