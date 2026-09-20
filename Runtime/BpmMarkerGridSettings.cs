namespace UnityEngine.Timeline
{
    internal readonly struct BpmMarkerGridSettings
    {
        // Properties

        internal double Bpm { get; }

        internal int BeatsPerBar { get; }

        internal BpmBeatUnit BeatUnit { get; }

        internal double BeatOriginSeconds { get; }

        internal Color BeatColor { get; }

        internal Color BarColor { get; }

        internal double BeatSeconds => 60d / Bpm;


        // Methods

        internal BpmMarkerGridSettings(
            double bpm,
            int beatsPerBar,
            BpmBeatUnit beatUnit,
            double beatOriginSeconds,
            Color beatColor,
            Color barColor)
        {
            Bpm = bpm;
            BeatsPerBar = beatsPerBar;
            BeatUnit = beatUnit;
            BeatOriginSeconds = beatOriginSeconds;
            BeatColor = beatColor;
            BarColor = barColor;
        }
    }
}
