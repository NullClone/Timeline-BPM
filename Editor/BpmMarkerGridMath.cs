using System;

namespace UnityEditor.Timeline
{
    internal static class BpmMarkerGridMath
    {
        private const double DoublePrecision = 2.2204460492503131e-16d;

        internal static bool TryGetVisibleTickStep(
            double pixelsPerBeat,
            int beatsPerBar,
            double minBeatSpacingPixels,
            double minBarSpacingPixels,
            out long tickStep)
        {
            if (double.IsNaN(pixelsPerBeat) || pixelsPerBeat <= 0d || beatsPerBar < 1)
            {
                tickStep = 0L;
                return false;
            }

            if (pixelsPerBeat >= minBeatSpacingPixels)
            {
                tickStep = 1L;
                return true;
            }

            if (pixelsPerBeat * beatsPerBar >= minBarSpacingPixels)
            {
                tickStep = beatsPerBar;
                return true;
            }

            tickStep = 0L;
            return false;
        }

        internal static bool TryGetVisibleBeatRange(
            double startTime,
            double endTime,
            double origin,
            double beatSeconds,
            long tickStep,
            out long firstBeat,
            out long lastBeat)
        {
            firstBeat = 0L;
            lastBeat = -1L;

            if (!IsFinite(startTime)
                || !IsFinite(endTime)
                || !IsFinite(origin)
                || !IsFinite(beatSeconds)
                || endTime <= startTime
                || beatSeconds <= 0d
                || tickStep < 1L)
            {
                return false;
            }

            var rawFirstBeat = NormalizeNearInteger((startTime - origin) / beatSeconds);
            var rawEndBeat = NormalizeNearInteger((endTime - origin) / beatSeconds);

            if (rawFirstBeat > long.MaxValue || rawEndBeat <= long.MinValue) return false;

            if (!TryAlignUp(CeilingToLong(rawFirstBeat), tickStep, out firstBeat)) return false;

            var endCeiling = CeilingToLong(rawEndBeat);
            var lastCandidate = endCeiling == long.MinValue ? long.MinValue : endCeiling - 1L;

            if (!TryAlignDown(lastCandidate, tickStep, out lastBeat)) return false;

            return lastBeat >= firstBeat;
        }

        internal static bool TryGetBeatTime(
            long beat,
            double origin,
            double beatSeconds,
            double visibleStart,
            double visibleEnd,
            out double time)
        {
            time = origin + beat * beatSeconds;
            return IsFinite(time) && time >= visibleStart && time < visibleEnd;
        }

        internal static bool IsBar(long beat, int beatsPerBar) =>
            beatsPerBar > 0 && PositiveModulo(beat, beatsPerBar) == 0L;

        private static double NormalizeNearInteger(double value)
        {
            if (!IsFinite(value)) return value;

            var nearestInteger = Math.Round(value);
            var tolerance = DoublePrecision * Math.Max(1d, Math.Abs(value)) * 2d;

            return Math.Abs(value - nearestInteger) <= tolerance ? nearestInteger : value;
        }

        private static long CeilingToLong(double value)
        {
            if (value >= long.MaxValue)
                return long.MaxValue;

            if (value <= long.MinValue)
                return long.MinValue;

            return (long)Math.Ceiling(value);
        }

        private static bool TryAlignUp(long value, long step, out long aligned)
        {
            if (step <= 1L)
            {
                aligned = value;
                return true;
            }

            var remainder = PositiveModulo(value, step);
            var delta = step - remainder;
            if (remainder != 0L && value > long.MaxValue - delta)
            {
                aligned = 0L;
                return false;
            }

            aligned = remainder == 0L ? value : value + delta;
            return true;
        }

        private static bool TryAlignDown(long value, long step, out long aligned)
        {
            if (step <= 1L)
            {
                aligned = value;
                return true;
            }

            var remainder = PositiveModulo(value, step);
            if (remainder != 0L && value < long.MinValue + remainder)
            {
                aligned = 0L;
                return false;
            }

            aligned = remainder == 0L ? value : value - remainder;
            return true;
        }

        private static long PositiveModulo(long value, long divisor)
        {
            var result = value % divisor;
            return result < 0L ? result + divisor : result;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
