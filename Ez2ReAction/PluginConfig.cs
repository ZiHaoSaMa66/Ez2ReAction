using IPA.Config.Stores;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace Ez2ReAction
{
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; }

        public virtual bool Enabled { get; set; } = false;
        public virtual float NjsOverride { get; set; } = 12f;

        public bool OverrideOffset { get; set; } = false;
        public float OffsetOverride { get; set; } = 0f;
        public float LastMapNjs { get; set; } = 12f;
        public float LastMapOffset { get; set; } = 0f;
        public float LastMapBpm { get; set; } = 120f;

        public string OriginalNjsLabel => $"{OriginalNjs:F1}";
        public float OriginalNjs { get; set; } = 0f;

        public string OriginalOffsetLabel => $"{OriginalOffset:F3}";
        public float OriginalOffset { get; set; } = 0f;

        public float OriginalBpm { get; set; } = 120f;

        public string ReactionTimeLabel
        {
            get
            {
                var njs = OriginalNjs > 0f ? OriginalNjs : LastMapNjs;
                var offset = Instance.OverrideOffset ? Instance.OffsetOverride : OriginalOffset;
                var bpm = OriginalBpm > 0f ? OriginalBpm : LastMapBpm;
                var ms = CalculateReactionTime(njs, offset, bpm);
                return $"{ms}ms";
            }
        }

        internal static int CalculateReactionTime(float njs, float offset, float bpm)
        {
            if (njs <= 0f || bpm <= 0f) return 0;
            var halfJump = 4f * bpm / 60f / njs;
            if (offset < 0)
                halfJump *= System.Math.Max(0.25f, 1f + offset);
            else
                halfJump *= System.Math.Max(0.25f, 1f + 2f * offset);
            return (int)System.Math.Round(halfJump * 2f * 1000f);
        }
    }
}
