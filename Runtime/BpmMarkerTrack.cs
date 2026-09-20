namespace UnityEngine.Timeline
{
    public sealed class BpmMarkerTrack : TrackAsset
    {
        // Fields

        [SerializeField]
        private double _bpm = 120d;

        [SerializeField]
        private int _beatsPerBar = 4;

        [SerializeField]
        private BpmBeatUnit _beatUnit = BpmBeatUnit.Quarter;

        [SerializeField]
        private double _beatOriginSeconds;

        [SerializeField]
        private Color _beatColor = new(0.9f, 0.9f, 0.9f, 0.5f);

        [SerializeField]
        private Color _barColor = new(0.9f, 0.9f, 0.9f, 0.6f);


        // Methods

        public override bool CanCreateTrackMixer() => false;

        internal bool TryGetGridSettings(out BpmMarkerGridSettings settings, out string error)
        {
            settings = default;

            if (!IsFinite(_bpm) || _bpm <= 0d)
            {
                error = "BPM must be a finite value greater than zero.";
                return false;
            }

            if (_beatsPerBar < 1)
            {
                error = "Beats Per Bar must be at least one.";
                return false;
            }

            if (!IsValidBeatUnit(_beatUnit))
            {
                error = "Beat Unit must be 1, 2, 4, 8, or 16.";
                return false;
            }

            if (!IsFinite(_beatOriginSeconds))
            {
                error = "Beat Origin must be a finite value.";
                return false;
            }

            if (!IsFinite(_beatColor) || !IsFinite(_barColor))
            {
                error = "Grid colors must contain finite values.";
                return false;
            }

            settings = new BpmMarkerGridSettings(
                _bpm,
                _beatsPerBar,
                _beatUnit,
                _beatOriginSeconds,
                _beatColor,
                _barColor);
            error = string.Empty;
            return true;
        }

        private static bool IsValidBeatUnit(BpmBeatUnit value) => value is
            BpmBeatUnit.Whole or
            BpmBeatUnit.Half or
            BpmBeatUnit.Quarter or
            BpmBeatUnit.Eighth or
            BpmBeatUnit.Sixteenth;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinite(Color value) => IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b) && IsFinite(value.a);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
