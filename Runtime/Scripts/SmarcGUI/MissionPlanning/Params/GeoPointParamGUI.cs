using SmarcGUI.WorldSpace;
using TMPro;
using UnityEngine;
using System.Collections.Generic;

namespace SmarcGUI.MissionPlanning.Params
{
    public class GeoPointParamGUI : ParamGUI, IParamHasXZ, IParamHasY, IParamHasTolerance
    {
        [Header("GeoPointParamGUI")]
        public TMP_InputField LatField, LonField, AltField, ToleranceField;

        public float altitude
        {
            get { return (float)((GeoPoint)paramValue).altitude; }
            set
            {
                var gp = (GeoPoint)paramValue;
                gp.altitude = value;
                paramValue = gp;
                AltField.text = value.ToString();
                NotifyPathChange();
            }
        }
        public double latitude
        {
            get { return ((GeoPoint)paramValue).latitude; }
            set
            {
                var gp = (GeoPoint)paramValue;
                gp.latitude = value;
                paramValue = gp;
                LatField.text = value.ToString();
                NotifyPathChange();
            }
        }
        public double longitude
        {
            get { return ((GeoPoint)paramValue).longitude; }
            set
            {
                var gp = (GeoPoint)paramValue;
                gp.longitude = value;
                paramValue = gp;
                LonField.text = value.ToString();
                NotifyPathChange();
            }
        }

        public float tolerance
        {
            get { return ((GeoPoint)paramValue).tolerance; }
            set
            {
                var gp = (GeoPoint)paramValue;
                gp.tolerance = value;
                paramValue = gp;
                ToleranceField.text = value.ToString();
                NotifyPathChange();
            }
        }


        protected override void SetupFields()
        {
            if (altitude == 0 && latitude == 0 && longitude == 0)
            {
                // set this to be the same as the previous geo point
                if (ParamIndex > 0)
                {
                    var previousGp = (GeoPoint)paramsList[ParamIndex - 1];
                    latitude = previousGp.latitude;
                    longitude = previousGp.longitude;
                    altitude = previousGp.altitude;
                    tolerance = previousGp.tolerance;
                    guiState.Log("New GeoPoint set to previous.");
                }
                // if there is no previous geo point, set it to where the camera is looking at
                else
                {
                    var point = guiState.GetLookAtPoint();
                    var (lat, lon) = GetLatLonFromUnityXZ(point.x, point.z);
                    latitude = lat;
                    longitude = lon;
                    altitude = point.y;
                    guiState.Log("New GeoPoint set to where the camera is looking at.");
                }
            }

            if (tolerance == 0) tolerance = 1;

            UpdateTexts();

            LatField.onEndEdit.AddListener(OnLatChanged);
            LonField.onEndEdit.AddListener(OnLonChanged);
            AltField.onEndEdit.AddListener(OnAltChanged);
            ToleranceField.onEndEdit.AddListener(OnToleranceChanged);


            fields.Add(LatField.GetComponent<RectTransform>());
            fields.Add(LonField.GetComponent<RectTransform>());
            fields.Add(AltField.GetComponent<RectTransform>());
            fields.Add(ToleranceField.GetComponent<RectTransform>());

            OnSelectedChange();
        }

        public override List<string> GetFieldLabels()
        {
            return new List<string> { "Lat", "Lon", "Alt", "Tol"  };
        }

        void UpdateTexts()
        {
            LatField.text = latitude.ToString();
            LonField.text = longitude.ToString();
            AltField.text = altitude.ToString();
            ToleranceField.text = tolerance.ToString();
        }

        void OnToleranceChanged(string s)
        {
            try { tolerance = float.Parse(s); }
            catch
            {
                guiState.Log("Invalid tolerance value");
                OnToleranceChanged(tolerance.ToString());
                return;
            }
            NotifyPathChange();
        }

        void OnLatChanged(string s)
        {
            try { latitude = double.Parse(s); }
            catch
            {
                guiState.Log("Invalid latitude value");
                OnLatChanged(latitude.ToString());
                return;
            }
            NotifyPathChange();
        }

        void OnLonChanged(string s)
        {
            try { longitude = double.Parse(s); }
            catch
            {
                guiState.Log("Invalid longitude value");
                OnLonChanged(longitude.ToString());
                return;
            }
            NotifyPathChange();
        }

        void OnAltChanged(string s)
        {
            try { altitude = float.Parse(s); }
            catch
            {
                guiState.Log("Invalid altitude value");
                OnAltChanged(altitude.ToString());
                return;
            }
            NotifyPathChange();
        }



        public (float, float) GetXZ()
        {
            var (tx, tz) = GetUnityXZFromLatLon(latitude, longitude);
            return ((float)tx, (float)tz);
        }

        public void SetXZ(float x, float z)
        {
            var (lat, lon) = GetLatLonFromUnityXZ(x, z);
            latitude = lat;
            longitude = lon;
        }

        public float GetY()
        {
            return altitude;
        }

        public float GetYReference()
        {
            return 0;
        }

        public void SetY(float y)
        {
            altitude = y;
        }
        
        public float GetTolerance()
        {
            return tolerance;
        }

        public void SetTolerance(float y)
        {
            tolerance = y;
        }
    }
}
