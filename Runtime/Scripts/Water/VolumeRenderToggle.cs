using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SmarcGUI.Water
{
    [RequireComponent(typeof(WaterSurface))]
    public class VolumeRenderToggle : WaterRenderToggle
    {
        private WaterSurface _surface;

        void Awake()
        {
            _surface = GetComponent<WaterSurface>();
        }
        public override void ToggleWaterRender(bool render)
        {
            _surface.enabled = render;
        }
    }

}