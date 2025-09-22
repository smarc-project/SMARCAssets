using SmarcGUI.MissionPlanning.Params;

namespace SmarcGUI.MissionPlanning.Tasks
{

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
            Params.Add("object_position", new GeoPoint());
            Params.Add("buoy_position", new GeoPoint());
            Params.Add("min_height_above_water", 3.0f);
            Params.Add("swoop_vertical", 5.0f);
            Params.Add("swoop_horizontal", 3.0f);
            Params.Add("straight_before_rope", 1.0f);
            Params.Add("straight_distance", 3.0f);
            Params.Add("raise_horizontal", 1.0f);
            Params.Add("raise_vertical", 10.0f);
        }
    }

    public class AlarsLocalize : Task
    {
        public override void SetParams()
        {
            Name = "alars-localize";
            Description = "Localize an AUV in the water by moving above the camera detection points";
            Params.Add("localize_auv", true);
            Params.Add("localize_buoy", true);
        }
    }

    public class AlarsCheckLoad : Task
    {
        public override void SetParams()
        {
            Name = "alars-check-load";
            Description = "Check if the AUV is hooked by measuring the load on the hook";
            Params.Add("check_duration", 2.0f);
            Params.Add("override_result", -1);
        }
    }
    
    

    

}