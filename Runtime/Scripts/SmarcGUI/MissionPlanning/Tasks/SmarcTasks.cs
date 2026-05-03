using System.Collections.Generic;
using SmarcGUI.MissionPlanning.Params;

namespace SmarcGUI.MissionPlanning.Tasks
{
    public class SmarcStartGeofence : Task
    {
        public override void SetParams()
        {
            Name = "smarc-start-geofence";
            Description = "Start a geofence task";
            Params.Add("waypoints", new List<GeoPoint>());
            Params.Add("ceiling_altitude", -1.0);
            Params.Add("floor_altitude", 1.0);
            Params.Add("stay_inside", true);
        }
    }

    public class SmarcStopGeofence : Task
    {
        public override void SetParams()
        {
            Name = "smarc-stop-geofence";
            Description = "Stop a geofence task";
            Params.Add("reset_geofence", true);
            Params.Add("reset_islands", true);
        }
    }

    public class GimbalSetRpy : Task
    {
        public override void SetParams()
        {
            Name = "gimbal-set-rpy";
            Description = "Set the roll, pitch, and yaw of the gimbal";
            Params.Add("roll", 0f);
            Params.Add("pitch", 0f);
            Params.Add("yaw", 0f);
        }
    }

    public class GimbalStop : Task
    {
        public override void SetParams()
        {
            Name = "gimbal-stop";
            Description = "Stop the gimbal";
        }
    }

    public class SmarcWait : Task
    {
        public override void SetParams()
        {
            Name = "smarc-wait";
            Description = "Wait for a specified amount of time";
        }
    }

    public class SmarcLog : Task
    {
        public override void SetParams()
        {
            Name = "smarc-log";
            Description = "Log a message to the topic smarc/human_log";
            Params.Add("log_str", "");
        }
    }
}
