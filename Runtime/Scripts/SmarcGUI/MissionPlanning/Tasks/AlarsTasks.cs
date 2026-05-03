using SmarcGUI.MissionPlanning.Params;

namespace SmarcGUI.MissionPlanning.Tasks
{

    public class AlarsTakeoff : Task
    {
        public override void SetParams()
        {
            Name = "alars-takeoff";
            Description = "Take off here.";
        }
    }

    public class AlarsLand : Task
    {
        public override void SetParams()
        {
            Name = "alars-land";
            Description = "Land here.";
        }
    }

    public class AlarsTakeControl : Task
    {
        public override void SetParams()
        {
            Name = "alars-take-control";
            Description = "Take control of the drone.";
        }
    }

    public class AlarsReleaseControl : Task
    {
        public override void SetParams()
        {
            Name = "alars-release-control";
            Description = "Release control of the drone.";
        }
    }

    public class AlarsBt : Task
    {
        public override void SetParams()
        {
            Name = "alars-bt";
            Description = "Run the entire ALARS system";
            Params.Add("num_retries", 5);
            Params.Add("search_position", new GeoPoint());
            Params.Add("forward_distance", 2.0f);
            Params.Add("forward_altitude", 3.0f);
            Params.Add("dipping_altitude", 7.0f);
            Params.Add("raising_altitude", 15.0f);
        }
    }

    public class AlarsSearch : Task
    {
        public override void SetParams()
        {
            Name = "alars-search";
            Description = "Search for an AUV in the water";
            Params.Add("search_position", new GeoPoint());
        }
    }

    public class AlarsRecover : Task
    {
        public override void SetParams()
        {
            Name = "alars-recover";
            Description = "Hook a rope in the water";
            Params.Add("forward_distance", 2.0f);
            Params.Add("forward_altitude", 3.0f);
            Params.Add("dipping_altitude", 7.0f);
            Params.Add("raising_altitude", 15.0f);
            Params.Add("no_buoy_radius", -1.0f);
        }
    }

    public class AlarsFollowAuv : Task
    {
        public override void SetParams()
        {
            Name = "alars-follow-auv";
            Description = "Follow an AUV in the water";
            Params.Add("follow_altitude", 15.0f);
            Params.Add("vulture_radius", 0.0f);
            Params.Add("vulture_speed_deg", 10.0f);
        }
    }

}