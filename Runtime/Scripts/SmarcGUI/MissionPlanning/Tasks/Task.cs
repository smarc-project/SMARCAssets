using System.Collections.Generic;
using Newtonsoft.Json;
using SmarcGUI.MissionPlanning.Params;
using System;
using UnityEngine;

namespace SmarcGUI.MissionPlanning.Tasks
{
    [JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.KebabCaseNamingStrategy))]
    public class Task
    {
        public string Name{get; set;}
        public string Description;
        public string TaskUuid;
        public Dictionary<string, object> Params;

        public static Dictionary<string, Type> GetAllKnownTaskTypes()
        {
            var taskType = typeof(Task);
            var d = new Dictionary<string, Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(taskType) && !type.IsAbstract)
                    {
                        d.Add(type.Name, type);
                    }
                }
            }
            return d;
        }

        public virtual void SetParams(){}

        public Task()
        {
            Params = new Dictionary<string, object>();
            SetParams();
            OnTaskModified();
        }

        public void OnTaskModified()
        {
            TaskUuid = Guid.NewGuid().ToString();
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public void RecoverFromJson()
        {
            // Cant modify a dictionary while iterating over it
            Dictionary<string, object> paramUpdates = new();

            // Dont like this, but we need to deserialize the params into their correct types
            // maybe we can do sth a little more elegant in the future...
            // this (wrongly) assumes that param types and param names are tied together
            // no such thing as "position" that is of type "latlon" etc.
            foreach (var param in Params)
            {
                var paramValue = param.Value;
                Type paramType = null;
                if(param.Key == "waypoint" || param.Key.Contains("position"))
                {
                    switch(Name)
                    {
                        case "move-to":
                            paramType = typeof(GeoPoint);
                            break;
                        case "alars-search":
                            paramType = typeof(GeoPoint);
                            break;
                        case "alars-recover":
                            paramType = typeof(GeoPoint);
                            break;
                        case "auv-depth-move-to":
                            paramType = typeof(AuvDepthPoint);
                            break;
                        case "auv-altitude-move-to":
                            paramType = typeof(AuvAltitudePoint);
                            break;
                        case "auv-hydrobatic-move-to":
                            paramType = typeof(AuvHydrobaticPoint);
                            break;
                        default:
                            break;
                    }
                }
                else if(param.Key == "waypoints")
                {
                    switch(Name)
                    {
                        case "move-path":
                            paramType = typeof(List<GeoPoint>);
                            break;
                        case "auv-depth-move-path":
                            paramType = typeof(List<AuvDepthPoint>);
                            break;
                        case "auv-altitude-move-path":
                            paramType = typeof(List<AuvAltitudePoint>);
                            break;
                        case "auv-hydrobatic-move-path":
                            paramType = typeof(List<AuvHydrobaticPoint>);
                            break;
                        default:
                            break;
                    }
                }

                if(paramType != null)
                {
                    var deserializedParam = JsonConvert.DeserializeObject(paramValue.ToString(), paramType);
                    paramUpdates.Add(param.Key, deserializedParam);
                }
                else
                {
                    // nothing specially handled, primitive types are handled by default
                    if (paramValue is string || paramValue is int || paramValue is float || paramValue is bool || paramValue is double || paramValue is long) continue;
                    

                    // not handled specially, and not a primitive...
                    // We don't know what this is... so we turn it into a string and show it
                    Debug.Log($"[Task] Unknown param type for param {param.Key} in task {Name}, turning into string. Type was: {paramValue.GetType()}");
                    paramUpdates.Add(param.Key, paramValue.ToString());
                }
            }

            foreach (var update in paramUpdates)
            {
                Params[update.Key] = update.Value;
            }
        }
       
    }

    
}