using CoordinateSharp;
using UnityEngine;


namespace GeoRef
{
    public enum OriginMode
    {
        LatLon,
        UTM
    }

    public enum UnityCoordinateFrame
    {
        UTM,
        WebMercator
    }

    public class GlobalReferencePoint : MonoBehaviour
    {
        [Tooltip("Use Lat/Lon or UTM as the origin and set the other values accordingly")]
        public OriginMode OriginMode = OriginMode.LatLon;
        [Tooltip("The global reference frame we assume the easting/norting of Unity to be in.")]
        public UnityCoordinateFrame UnityCoordinateFrame = UnityCoordinateFrame.UTM;

        [Header("Lat/lon in decimal degrees")]
        public double Lon = 17.596178; // asko bottom left defaults
        public double Lat = 58.811481;

        [Header("UTM properties")]
        public string UTMBand;
        public int UTMZone;
        public double UTMEasting;
        public double UTMNorthing;

        [Header("Web Mercator properties")]
        public double WebMercatorEasting;
        public double WebMercatorNorthing;

        EagerLoad el;
        EagerLoad elWeb;


        void OnValidate()
        {
            if (OriginMode == OriginMode.LatLon)
            {
                var latlon = new Coordinate(Lat, Lon);
                UTMEasting = latlon.UTM.Easting;
                UTMNorthing = latlon.UTM.Northing;
                UTMBand = latlon.UTM.LatZone;  //str
                UTMZone = latlon.UTM.LongZone; //int
            }
            else
            {
                (Lat, Lon) = GetLatLonFromUTM(UTMEasting, UTMNorthing);
            }

            var ll = new Coordinate(Lat, Lon);
            elWeb ??= new(EagerLoadType.WebMercator);
            var webmerc = ll.WebMercator;
            WebMercatorEasting = webmerc.Easting;
            WebMercatorNorthing = webmerc.Northing;
        }

        void Awake()
        {
            // Turn off eager loading of everything except UTM stuff
            // until we need to do things in space, we dont need to load the whole CoordinateSharp library
            // passed to things that work with Coordinate objects
            // Hopefully cuts down on processing time _a little_ for things in a loop :)
            el = new(EagerLoadType.UTM_MGRS);

            var refpoints = FindObjectsByType<GlobalReferencePoint>(FindObjectsSortMode.None);
            if (refpoints.Length > 1)
            {
                Debug.LogWarning("Found too many GlobalReferencePoint in the scene, there should only be one!");
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            float size = 500;
            var top = new Vector3(transform.position.x, size, transform.position.z);
            var bottom = new Vector3(transform.position.x, -size, transform.position.z);
            Gizmos.DrawSphere(top, 10f);
            Gizmos.DrawSphere(bottom, 10f);
            Gizmos.DrawLine(top, bottom);
        }

        public void UpdateGeoRefObjects()
        {
            var gtfers = FindObjectsByType<GeoReferenceTransformer>(FindObjectsSortMode.None);
            foreach (var gtfer in gtfers)
            {
                gtfer.TransformFromTwoPoints();
            }

            var georefs = FindObjectsByType<GeoReference>(FindObjectsSortMode.None);
            foreach (var georef in georefs)
            {
                georef.Place();
            }
        }

        public (double, double) GetLatLonFromUTM(double easting, double northing)
        {
            el ??= new(EagerLoadType.UTM_MGRS);
            var utm = new UniversalTransverseMercator(UTMBand, UTMZone, easting, northing);
            var latlon = UniversalTransverseMercator.ConvertUTMtoLatLong(utm, el);
            return (latlon.Latitude.ToDouble(), latlon.Longitude.ToDouble());
        }

        public (double easting, double northing, double lat, double lon) GetUTMLatLonOfObject(GameObject o)
        {
            var posDiff = o.transform.position - transform.position;
            var xDiff = posDiff.x;
            var zDiff = posDiff.z;
            // +z = north
            // +x = east
            var obj_easting = UTMEasting + xDiff;
            var obj_northing = UTMNorthing + zDiff;
            (var obj_lat, var obj_lon) = GetLatLonFromUTM(obj_easting, obj_northing);
            return (obj_easting, obj_northing, obj_lat, obj_lon);
        }

        public (float x, float z) GetUnityXZFromLatLon(double lat, double lon)
        {
            if (UnityCoordinateFrame == UnityCoordinateFrame.WebMercator)
            {
                elWeb ??= new(EagerLoadType.WebMercator);
                var latlon = new Coordinate(lat, lon, elWeb);
                var webmerc = latlon.WebMercator;
                var eastingDiff = webmerc.Easting - WebMercatorEasting;
                var northingDiff = webmerc.Northing - WebMercatorNorthing;
                var unityX = transform.position.x + eastingDiff;
                var unityZ = transform.position.z + northingDiff;
                return ((float)unityX, (float)unityZ);
            }
            else
            {
                el ??= new(EagerLoadType.UTM_MGRS);
                var latlon = new Coordinate(lat, lon, el);
                var eastingDiff = latlon.UTM.Easting - UTMEasting;
                var northingDiff = latlon.UTM.Northing - UTMNorthing;
                var unityX = transform.position.x + eastingDiff;
                var unityZ = transform.position.z + northingDiff;
                return ((float)unityX, (float)unityZ);
            }
        }



        public (double lat, double lon) GetLatLonFromUnityXZ(float x, float z)
        {
            if (UnityCoordinateFrame == UnityCoordinateFrame.WebMercator)
            {
                elWeb ??= new(EagerLoadType.WebMercator);
                var eastingDiff = x - transform.position.x;
                var northingDiff = z - transform.position.z;
                var webmerc_easting = WebMercatorEasting + eastingDiff;
                var webmerc_northing = WebMercatorNorthing + northingDiff;
                (var lat, var lon) = GetLatLonFromWebMercator(webmerc_easting, webmerc_northing);
                return (lat, lon);
            }
            else
            {
                var eastingDiff = x - transform.position.x;
                var northingDiff = z - transform.position.z;
                var utm_easting = UTMEasting + eastingDiff;
                var utm_northing = UTMNorthing + northingDiff;
                (var lat, var lon) = GetLatLonFromUTM(utm_easting, utm_northing);
                return (lat, lon);
            }
        }

        public (double easting, double northing) GetWebMercatorFromLatLon(double lat, double lon)
        {
            elWeb ??= new(EagerLoadType.WebMercator);
            var latlon = new Coordinate(lat, lon, elWeb);
            var webmerc = latlon.WebMercator;
            return (webmerc.Easting, webmerc.Northing);
        }

        public (double lat, double lon) GetLatLonFromWebMercator(double easting, double northing)
        {
            elWeb ??= new(EagerLoadType.WebMercator);
            var webmerc = new WebMercator(easting, northing);
            var latlon = WebMercator.ConvertWebMercatortoLatLong(webmerc, elWeb);
            return (latlon.Latitude.ToDouble(), latlon.Longitude.ToDouble());
        }
    }
    
}