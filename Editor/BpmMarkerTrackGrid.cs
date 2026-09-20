using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace UnityEditor.Timeline
{
    /// <summary>
    /// BPMマーカーグリッドの描画およびスナップ処理を管理するクラス。
    /// スナップ処理はUnity Timeline標準の仕様に準拠し、可視範囲内（画面内）にあるトラックに対して実行されます。
    /// </summary>
    [InitializeOnLoad]
    internal static class BpmMarkerTrackGrid
    {
        // Fields

        private const string ElementName = "bpm-marker-track-grid";
        private const int InitialSnapPointCapacity = 1024;
        private const float MinBeatSpacingPixels = 12f;
        private const float MinBarSpacingPixels = 16f;
        private const float SnapBoundsWidth = 1f;

        private static readonly List<BpmSnapPointGUI> SnapPoints = new(InitialSnapPointCapacity);
        private static readonly List<TrackViewState> LastTrackViewStates = new();
        private static readonly List<TrackViewState> CurrentTrackViewStates = new();

        private static TimelineWindow attachedWindow;
        private static IMGUIContainer container;
        private static double lastAttachCheck;
        private static TimelineViewState lastTimelineViewState;
        private static bool hasLastViewState;
        private static bool repaintRequested = true;


        // Methods

        static BpmMarkerTrackGrid()
        {
            EditorApplication.update += Update;
            EditorApplication.hierarchyChanged += RequestRepaint;
            EditorApplication.projectChanged += RequestRepaint;

            Undo.undoRedoPerformed += RequestRepaint;
        }

        private static void Update()
        {
            if (!IsAttachedWindowAlive()) Detach();

            var now = EditorApplication.timeSinceStartup;
            if (now - lastAttachCheck >= 0.1d)
            {
                lastAttachCheck = now;
                AttachIfNeeded();
            }

            RepaintIfViewChanged();
        }

        private static void AttachIfNeeded()
        {
            var window = TimelineWindow.instance;
            if (window == null)
            {
                Detach();
                return;
            }

            RemoveStaleElements(window);

            if (ReferenceEquals(window, attachedWindow) && container != null) return;

            Detach();

            attachedWindow = window;
            container = new IMGUIContainer(DrawAndRegisterVisibleBeatGrids)
            {
                name = ElementName,
                pickingMode = PickingMode.Ignore,
            };
            container.style.position = Position.Absolute;
            container.style.left = 0;
            container.style.top = 0;
            container.style.right = 0;
            container.style.bottom = 0;

            window.rootVisualElement.Add(container);

            ResetViewState();
            InvalidateTimelineAndOverlay();
        }

        private static void Detach()
        {
            container?.RemoveFromHierarchy();

            container = null;
            attachedWindow = null;
            SnapPoints.Clear();
            ResetViewState();
        }

        internal static void RequestRepaint()
        {
            repaintRequested = true;
            InvalidateTimelineAndOverlay();
        }

        private static void InvalidateTimelineAndOverlay()
        {
            attachedWindow?.Repaint();
            container?.MarkDirtyRepaint();
        }

        private static void RepaintIfViewChanged()
        {
            if (container == null || attachedWindow?.state == null) return;

            if (!TimelineViewState.TryCapture(attachedWindow, out var currentTimelineViewState)
                || !TryCaptureTrackViewStates(attachedWindow))
            {
                return;
            }

            var viewChanged = !hasLastViewState
                              || !lastTimelineViewState.Equals(currentTimelineViewState)
                              || !TrackViewStatesEqual(LastTrackViewStates, CurrentTrackViewStates);

            if (!repaintRequested && !viewChanged) return;

            lastTimelineViewState = currentTimelineViewState;
            hasLastViewState = true;
            CopyTrackViewStates(CurrentTrackViewStates, LastTrackViewStates);
            repaintRequested = false;
            InvalidateTimelineAndOverlay();
        }

        private static void ResetViewState()
        {
            hasLastViewState = false;
            repaintRequested = true;
            LastTrackViewStates.Clear();
            CurrentTrackViewStates.Clear();
        }

        private static bool TryCaptureTrackViewStates(TimelineWindow window)
        {
            CurrentTrackViewStates.Clear();

            try
            {
                var allTracks = window.allTracks;
                if (allTracks == null) return false;

                foreach (var trackGui in allTracks)
                {
                    if (trackGui.track is BpmMarkerTrack track)
                    {
                        CurrentTrackViewStates.Add(TrackViewState.Capture(trackGui, track));
                    }
                }

                return true;
            }
            catch (NullReferenceException)
            {
                CurrentTrackViewStates.Clear();
                return false;
            }
            catch (MissingReferenceException)
            {
                CurrentTrackViewStates.Clear();
                return false;
            }
        }

        private static bool TrackViewStatesEqual(List<TrackViewState> left, List<TrackViewState> right)
        {
            if (left.Count != right.Count) return false;

            for (var index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void CopyTrackViewStates(List<TrackViewState> source, List<TrackViewState> destination)
        {
            destination.Clear();
            destination.AddRange(source);
        }

        private static bool IsAttachedWindowAlive()
        {
            return attachedWindow != null
                   && attachedWindow.rootVisualElement != null
                   && attachedWindow.rootVisualElement.panel != null;
        }

        private static void RemoveStaleElements(TimelineWindow window)
        {
            if (window?.rootVisualElement == null) return;

            while (true)
            {
                var element = window.rootVisualElement.Q<VisualElement>(ElementName);
                if (element == null || ReferenceEquals(element, container)) return;

                element.RemoveFromHierarchy();
            }
        }

        private static bool TryGetVisibleTrackArea(TimelineWindow window, out Rect area)
        {
            area = default;
            var state = window?.state;
            if (state == null) return false;

            var contentRect = window.sequenceContentRect;
            var yMin = WindowConstants.markerRowYPosition + (state.showMarkerHeader ? WindowConstants.markerRowHeight : 0f);
            var yMax = contentRect.yMax;

            if (yMax <= yMin || contentRect.width <= 0f) return false;

            area = Rect.MinMaxRect(contentRect.xMin, yMin, contentRect.xMax, yMax);
            return true;
        }

        private static void DrawAndRegisterVisibleBeatGrids()
        {
            if (Event.current.type != EventType.Repaint) return;

            var window = attachedWindow;
            if (window?.state == null) return;

            var state = window.state;
            IReadOnlyList<TimelineTrackBaseGUI> allTracks;
            try
            {
                allTracks = window.allTracks;
            }
            catch (NullReferenceException)
            {
                return;
            }
            catch (MissingReferenceException)
            {
                return;
            }

            if (allTracks == null) return;

            if (!TryGetVisibleTrackArea(window, out var visibleTrackArea))
                return;

            using (new GUIViewportScope(visibleTrackArea))
            {
                var snapPointIndex = 0;
                foreach (var trackGui in allTracks)
                {
                    if (!trackGui.visibleRow || trackGui.track is not BpmMarkerTrack track)
                        continue;

                    if (!track.TryGetGridSettings(out var settings, out _))
                        continue;

                    if (track.mutedInHierarchy)
                        continue;

                    var trackRect = trackGui.boundingRect;
                    if (trackRect.width <= 0f || trackRect.height <= 0f)
                        continue;

                    var gridRect = GetGridRect(trackRect, visibleTrackArea);
                    if (gridRect.width <= 0f)
                        continue;

                    var visibleRange = new Vector2(
                        state.PixelToTime(gridRect.xMin),
                        state.PixelToTime(gridRect.xMax));
                    if (visibleRange.y <= visibleRange.x)
                        continue;

                    var beatSeconds = settings.BeatSeconds;
                    if (!IsFinite(beatSeconds) || beatSeconds <= 0d)
                        continue;

                    if (!TryGetVisibleTickStep(state, settings, beatSeconds, out var tickStep))
                        continue;

                    DrawGrid(state, gridRect, visibleRange, settings, beatSeconds, tickStep);
                    RegisterSnapPoints(state, visibleRange, settings, beatSeconds, tickStep, ref snapPointIndex);
                }
            }
        }

        private static void DrawGrid(
            WindowState state,
            Rect gridRect,
            Vector2 visibleRange,
            BpmMarkerGridSettings settings,
            double beatSeconds,
            long tickStep)
        {
            if (!BpmMarkerGridMath.TryGetVisibleBeatRange(
                    visibleRange.x,
                    visibleRange.y,
                    settings.BeatOriginSeconds,
                    beatSeconds,
                    tickStep,
                    out var firstBeat,
                    out var lastBeat))
            {
                return;
            }

            GUI.BeginClip(gridRect);
            for (var beat = firstBeat; beat <= lastBeat; beat += tickStep)
            {
                if (!BpmMarkerGridMath.TryGetBeatTime(
                        beat,
                        settings.BeatOriginSeconds,
                        beatSeconds,
                        visibleRange.x,
                        visibleRange.y,
                        out var time))
                {
                    if (beat > long.MaxValue - tickStep)
                        break;

                    continue;
                }

                var windowPixel = state.TimeToPixel(time);
                if (!IsFinite(windowPixel))
                {
                    if (beat > long.MaxValue - tickStep)
                        break;

                    continue;
                }

                var x = windowPixel - gridRect.x;
                var isBar = BpmMarkerGridMath.IsBar(beat, settings.BeatsPerBar);
                var height = isBar ? gridRect.height * 0.7f : gridRect.height * 0.5f;
                var width = isBar ? 1.5f : 1f;
                var color = isBar ? settings.BarColor : settings.BeatColor;

                EditorGUI.DrawRect(
                    new Rect(Mathf.Round(x) - width * 0.5f, 0f, width, height),
                    color);

                if (beat > long.MaxValue - tickStep) break;
            }

            GUI.EndClip();
        }

        private static bool TryGetVisibleTickStep(
            WindowState state,
            BpmMarkerGridSettings settings,
            double beatSeconds,
            out long tickStep)
        {
            var pixelsPerBeat = beatSeconds * Math.Abs(state.timeAreaScale.x);
            return BpmMarkerGridMath.TryGetVisibleTickStep(
                pixelsPerBeat,
                settings.BeatsPerBar,
                MinBeatSpacingPixels,
                MinBarSpacingPixels,
                out tickStep);
        }

        private static Rect GetGridRect(Rect trackRect, Rect timeAreaRect)
        {
            trackRect.xMin = Math.Max(trackRect.xMin, timeAreaRect.xMin);
            trackRect.xMax = Math.Min(trackRect.xMax, timeAreaRect.xMax);
            return trackRect;
        }

        private static void RegisterSnapPoints(
            WindowState state,
            Vector2 visibleRange,
            BpmMarkerGridSettings settings,
            double beatSeconds,
            long tickStep,
            ref int snapPointIndex)
        {
            if (!BpmMarkerGridMath.TryGetVisibleBeatRange(
                    visibleRange.x,
                    visibleRange.y,
                    settings.BeatOriginSeconds,
                    beatSeconds,
                    tickStep,
                    out var firstBeat,
                    out var lastBeat))
            {
                return;
            }

            var yMin = state.timeAreaRect.yMax;
            var height = Math.Max(1f, state.windowHeight - yMin);
            for (var beat = firstBeat; beat <= lastBeat; beat += tickStep)
            {
                if (BpmMarkerGridMath.TryGetBeatTime(
                        beat,
                        settings.BeatOriginSeconds,
                        beatSeconds,
                        visibleRange.x,
                        visibleRange.y,
                        out var time)
                    && time >= 0d)
                {
                    EnsureSnapPointCount(snapPointIndex + 1);

                    var point = SnapPoints[snapPointIndex++];
                    point.SetTime(time);

                    var x = state.TimeToPixel(time);
                    if (IsFinite(x))
                    {
                        var bounds = new Rect(x - SnapBoundsWidth * 0.5f, yMin, SnapBoundsWidth, height);
                        state.spacePartitioner.AddBounds(point, bounds);
                    }
                }

                if (beat > long.MaxValue - tickStep)
                    break;
            }
        }

        private static void EnsureSnapPointCount(int count)
        {
            while (SnapPoints.Count < count)
                SnapPoints.Add(new BpmSnapPointGUI());
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);


        // Classes

        private readonly struct TimelineViewState : IEquatable<TimelineViewState>
        {
            private readonly Vector2 _timeAreaTranslation;
            private readonly Vector2 _timeAreaScale;
            private readonly Vector2 _shownRange;
            private readonly Rect _timeAreaRect;
            private readonly Vector2 _windowSize;
            private readonly Vector2 _treeScrollPosition;
            private readonly bool _showMarkerHeader;

            private TimelineViewState(
                Vector2 timeAreaTranslation,
                Vector2 timeAreaScale,
                Vector2 shownRange,
                Rect timeAreaRect,
                Vector2 windowSize,
                Vector2 treeScrollPosition,
                bool showMarkerHeader)
            {
                _timeAreaTranslation = timeAreaTranslation;
                _timeAreaScale = timeAreaScale;
                _shownRange = shownRange;
                _timeAreaRect = timeAreaRect;
                _windowSize = windowSize;
                _treeScrollPosition = treeScrollPosition;
                _showMarkerHeader = showMarkerHeader;
            }

            internal static bool TryCapture(TimelineWindow window, out TimelineViewState viewState)
            {
                viewState = default;
                try
                {
                    var state = window.state;
                    if (state == null)
                        return false;

                    var treeView = window.treeView;
                    var treeScrollPosition = treeView != null ? treeView.scrollPosition : Vector2.zero;
                    viewState = new TimelineViewState(
                        state.timeAreaTranslation,
                        state.timeAreaScale,
                        state.timeAreaShownRange,
                        state.timeAreaRect,
                        window.position.size,
                        treeScrollPosition,
                        state.showMarkerHeader);
                    return true;
                }
                catch (NullReferenceException)
                {
                    return false;
                }
                catch (MissingReferenceException)
                {
                    return false;
                }
            }

            public bool Equals(TimelineViewState other) =>
                _timeAreaTranslation == other._timeAreaTranslation
                && _timeAreaScale == other._timeAreaScale
                && _shownRange == other._shownRange
                && _timeAreaRect == other._timeAreaRect
                && _windowSize == other._windowSize
                && _treeScrollPosition == other._treeScrollPosition
                && _showMarkerHeader == other._showMarkerHeader;
        }

        private readonly struct TrackViewState : IEquatable<TrackViewState>
        {
            private readonly BpmMarkerTrack _track;
            private readonly bool _visibleRow;
            private readonly Rect _boundingRect;
            private readonly bool _hasValidSettings;
            private readonly double _bpm;
            private readonly int _beatsPerBar;
            private readonly BpmBeatUnit _beatUnit;
            private readonly double _beatOriginSeconds;
            private readonly Color _beatColor;
            private readonly Color _barColor;

            private TrackViewState(
                BpmMarkerTrack track,
                bool visibleRow,
                Rect boundingRect,
                bool hasValidSettings,
                BpmMarkerGridSettings settings)
            {
                _track = track;
                _visibleRow = visibleRow;
                _boundingRect = boundingRect;
                _hasValidSettings = hasValidSettings;
                _bpm = settings.Bpm;
                _beatsPerBar = settings.BeatsPerBar;
                _beatUnit = settings.BeatUnit;
                _beatOriginSeconds = settings.BeatOriginSeconds;
                _beatColor = settings.BeatColor;
                _barColor = settings.BarColor;
            }

            internal static TrackViewState Capture(TimelineTrackBaseGUI trackGui, BpmMarkerTrack track)
            {
                var hasValidSettings = track.TryGetGridSettings(out var settings, out _);
                return new TrackViewState(track, trackGui.visibleRow, trackGui.boundingRect, hasValidSettings, settings);
            }

            public bool Equals(TrackViewState other) =>
                ReferenceEquals(_track, other._track)
                && _visibleRow == other._visibleRow
                && _boundingRect == other._boundingRect
                && _hasValidSettings == other._hasValidSettings
                && _bpm.Equals(other._bpm)
                && _beatsPerBar == other._beatsPerBar
                && _beatUnit == other._beatUnit
                && _beatOriginSeconds.Equals(other._beatOriginSeconds)
                && _beatColor.Equals(other._beatColor)
                && _barColor.Equals(other._barColor);
        }

        private sealed class BpmSnapPointGUI : TimelineItemGUI, ISnappable
        {
            private static readonly BpmSnapRow Row = new();
            private double time;

            public BpmSnapPointGUI()
                : base(Row)
            {
                visible = false;
            }

            public override ITimelineItem item => null;
            public override double start => time;
            public override double end => time;

            public void SetTime(double value)
            {
                time = value;
            }

            public IEnumerable<Edge> SnappableEdgesFor(IAttractable attractable, ManipulateEdges manipulateEdges)
            {
                yield return new Edge(time);
            }

            public override void Draw(Rect rect, bool rectChanged, WindowState state) { }

            public override Rect RectToTimeline(Rect trackRect, WindowState state) => Rect.zero;

            public override bool CanSelect(Event evt) => false;
        }

        private sealed class BpmSnapRow : IRowGUI
        {
            public TrackAsset asset => null;
            public Rect boundingRect => Rect.zero;
            public bool locked => false;
            public bool showMarkers => false;
            public bool muted => false;

            public Rect ToWindowSpace(Rect treeViewRect) => treeViewRect;
        }
    }
}
