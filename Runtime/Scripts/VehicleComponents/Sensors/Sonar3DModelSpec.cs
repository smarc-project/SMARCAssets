namespace VehicleComponents.Sensors
{
    // ONNX tensor contract shared by SonarReturnModel and Sonar3D.
    // Keep this separate from the Unity component so the inference wrapper does not depend on Sonar3D.
    internal static class Sonar3DModelSpec
    {
        // v1 contract for the deployed return-channel U-Net (sonar_model.onnx).
        // Image height/width match the Water Linked 3D-15 sensor geometry and are not expected to change.
        // Input/output channel counts and per-channel semantics may change when a new model is deployed;
        // add Sonar3DModelSpec.V2 (or bump this block) and update the tensor build/decode paths accordingly.
        public static class V1
        {
            public const int ImageHeight = 64;
            public const int ImageWidth = 256;
            public const int PixelCount = ImageHeight * ImageWidth;

            public const int InputChannels = 4;
            public const int OutputChannels = 3;

            // Planar NCHW layout: buffer[channel * PixelCount + pixelIndex].
            public const int InputAzimuthChannel = 0;
            public const int InputElevationChannel = 1;
            public const int InputRangeChannel = 2;
            public const int InputCosIncidenceChannel = 3;

            public const int OutputDetectionChannel = 0;
            public const int OutputRangeResidualChannel = 1;
            public const int OutputLogIntensityChannel = 2;
        }
    }
}
