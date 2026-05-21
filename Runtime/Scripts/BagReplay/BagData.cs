using System.Globalization;
using CsvHelper.Configuration;
using DefaultNamespace;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

namespace BagReplay
{
    public class BagData
    {
        public float Vbs_cmd { get; set; }
        public float Lcg_cmd { get; set; }
        public int Thruster1RPM { get; set; }
        public int Thruster2RPM { get; set; }
        public float ThrusterHorizontalRad { get; set; }
        public float ThrusterVerticalRad { get; set; }
        public Vector3 PositionMocapFRD { get; set; }
        public Quaternion OrientationMocapFRD { get; set; }
        public Vector3 LinearVelocityMocapFRD { get; set; }
        public Vector3 AngularVelocityMocapFRD { get; set; }
        public float Vbs_fb { get; set; }
        public float Lcg_fb { get; set; }
        public Vector3 LinearVelocityBodyFRD { get; set; }
        public Vector3 AngularVelocityBodyFRD { get; set; }

        public BagCsvRow ToCsv(Transform helperTransform, float time)
        {
            var bagCsvRow = new BagCsvRow();
            bagCsvRow.Vbs_cmd = Vbs_cmd;
            bagCsvRow.Vbs_fb = Vbs_fb;
            bagCsvRow.Lcg_cmd = Lcg_cmd;
            bagCsvRow.Lcg_fb = Lcg_fb;
            bagCsvRow.Thruster1RPM = Thruster1RPM;
            bagCsvRow.Thruster2RPM = Thruster2RPM;
            bagCsvRow.ThrusterHorizontalRad = ThrusterHorizontalRad;
            bagCsvRow.ThrusterVerticalRad = ThrusterVerticalRad;

            bagCsvRow.OrientationMocapFRD = OrientationMocapFRD;
            helperTransform.rotation = FRD.ConvertToRUF(OrientationMocapFRD);

            bagCsvRow.PositionMocapFRD = PositionMocapFRD;
            bagCsvRow.AnglesMocapFRD = FRD.ConvertFromRUF(AngleUtils.NormalizeAngles(helperTransform.rotation.eulerAngles)) * Mathf.Deg2Rad;

            bagCsvRow.LinearVelocityBodyFRD = LinearVelocityBodyFRD; //FRD.ConvertFromRUF(helperTransform.InverseTransformVector(FRD.ConvertToRUF(LinearVelocityMocapFRD)));
            bagCsvRow.AngularVelocityBodyFRD = AngularVelocityBodyFRD; //FRD.ConvertAngularVelocityFromRUF(helperTransform.InverseTransformVector(FRD.ConvertAngularVelocityToRUF(AngularVelocityMocapFRD)));
            bagCsvRow.LinearVelocityMocapFRD = LinearVelocityMocapFRD;
            bagCsvRow.AngularVelocityMocapFRD = AngularVelocityMocapFRD;
            bagCsvRow.Time = time;

            return bagCsvRow;
        }
    }

    public class BagCsvRow
    {
        public float Time { get; set; }
        public Quaternion OrientationMocapFRD { get; set; }
        public float Vbs_cmd { get; set; }
        public float Lcg_cmd { get; set; }
        public float Thruster1RPM { get; set; }
        public float Thruster2RPM { get; set; }
        public float ThrusterHorizontalRad { get; set; }
        public float ThrusterVerticalRad { get; set; }
        public float Vbs_fb { get; set; }
        public float Lcg_fb { get; set; }
        public Vector3 PositionMocapFRD { get; set; }
        public Vector3 AnglesMocapFRD { get; set; }
        public Vector3 AngularVelocityBodyFRD { get; set; }
        public Vector3 LinearVelocityBodyFRD { get; set; }
        public Vector3 LinearVelocityMocapFRD { get; set; }
        public Vector3 AngularVelocityMocapFRD { get; set; }
    }

    public sealed class BagCsvRowMap : ClassMap<BagCsvRow>
    {
        public BagCsvRowMap()
        {
            Map(m => m.LinearVelocityMocapFRD).Ignore();
            Map(m => m.LinearVelocityBodyFRD).Ignore();
            Map(m => m.AngularVelocityMocapFRD).Ignore();
            Map(m => m.AngularVelocityBodyFRD).Ignore();
            Map(m => m.PositionMocapFRD).Ignore();
            Map(m => m.OrientationMocapFRD).Ignore();
            Map(m => m.AnglesMocapFRD).Ignore();

            Map(m => m.OrientationMocapFRD.x).Name("OrientationFRD_MOCAP_X").Index(0);
            Map(m => m.OrientationMocapFRD.y).Name("OrientationFRD_MOCAP_Y").Index(1);
            Map(m => m.OrientationMocapFRD.z).Name("OrientationFRD_MOCAP_Z").Index(2);
            Map(m => m.OrientationMocapFRD.w).Name("OrientationFRD_MOCAP_W").Index(3);

            Map(m => m.LinearVelocityMocapFRD.x).Name("LinVelFRD_MOCAP_X").Index(4);
            Map(m => m.LinearVelocityMocapFRD.y).Name("LinVelFRD_MOCAP_Y").Index(5);
            Map(m => m.LinearVelocityMocapFRD.z).Name("LinVelFRD_MOCAP_Z").Index(6);

            Map(m => m.AngularVelocityMocapFRD.x).Name("AngVelFRD_MOCAP_X").Index(7);
            Map(m => m.AngularVelocityMocapFRD.y).Name("AngVelFRD_MOCAP_Y").Index(8);
            Map(m => m.AngularVelocityMocapFRD.z).Name("AngVelFRD_MOCAP_Z").Index(9);

            Map(m => m.ThrusterHorizontalRad).Index(10);
            Map(m => m.ThrusterVerticalRad).Index(11);
            Map(m => m.Vbs_cmd).Index(12);
            Map(m => m.Lcg_cmd).Index(13);
            Map(m => m.Thruster1RPM).Index(14);
            Map(m => m.Thruster2RPM).Index(15);
            Map(m => m.Vbs_fb).Index(16);
            Map(m => m.Lcg_fb).Index(17);
            
            Map(m => m.AnglesMocapFRD.x).Name("OrientationEulerRadFRD_MOCAP_X").Index(18);
            Map(m => m.AnglesMocapFRD.y).Name("OrientationEulerRadFRD_MOCAP_Y").Index(19);
            Map(m => m.AnglesMocapFRD.z).Name("OrientationEulerRadFRD_MOCAP_Z").Index(20);
            
            Map(m => m.PositionMocapFRD.x).Name("PositionFRD_MOCAP_X").Index(21);
            Map(m => m.PositionMocapFRD.y).Name("PositionFRD_MOCAP_Y").Index(22);
            Map(m => m.PositionMocapFRD.z).Name("PositionFRD_MOCAP_Z").Index(23);

            Map(m => m.LinearVelocityBodyFRD.x).Name("LinVelFRD_BODY_X").Index(24);
            Map(m => m.LinearVelocityBodyFRD.y).Name("LinVelFRD_BODY_Y").Index(25);
            Map(m => m.LinearVelocityBodyFRD.z).Name("LinVelFRD_BODY_Z").Index(26);

            Map(m => m.AngularVelocityBodyFRD.x).Name("AngVelFRD_BODY_X").Index(27);
            Map(m => m.AngularVelocityBodyFRD.y).Name("AngVelFRD_BODY_Y").Index(28);
            Map(m => m.AngularVelocityBodyFRD.z).Name("AngVelFRD_BODY_Z").Index(29);

            Map(m => m.Time).Index(30);
        }
    }
}