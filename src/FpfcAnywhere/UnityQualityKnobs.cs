using FpfcAnywhere.Core;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace FpfcAnywhere
{
    /// <summary>
    /// The game puts its VR resolution scale and anti-aliasing level on the URP asset, so the
    /// desktop window is drawn at them too. Those two values on that asset are the only things
    /// the mod writes outside its own objects apart from the menu pointer, only while the free
    /// camera is showing, and QualityOverride puts them back. The cap is the mod's own limiter,
    /// so nothing of the game's is written for it. With a headset running the values would reach
    /// the headset as well, so nothing is touched then.
    /// </summary>
    internal sealed class UnityQualityKnobs : IQualityKnobs
    {
        private readonly FrameLimiter _limiter;

        public UnityQualityKnobs(FrameLimiter limiter)
        {
            _limiter = limiter;
        }

        public bool CanOverride => Asset != null && !XRSettings.isDeviceActive;

        public float RenderScale
        {
            get
            {
                var asset = Asset;
                return asset != null ? asset.renderScale : 0f;
            }

            set
            {
                var asset = Asset;
                if (asset != null)
                {
                    asset.renderScale = value;
                }
            }
        }

        public int AntiAliasing
        {
            get
            {
                var asset = Asset;
                return asset != null ? asset.msaaSampleCount : 0;
            }

            set
            {
                var asset = Asset;
                if (asset != null)
                {
                    asset.msaaSampleCount = value;
                }
            }
        }

        public int FpsCap
        {
            get => _limiter.FramesPerSecond;
            set => _limiter.FramesPerSecond = value;
        }

        private static UniversalRenderPipelineAsset? Asset => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
    }
}
