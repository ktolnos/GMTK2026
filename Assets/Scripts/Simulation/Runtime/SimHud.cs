using System.Collections.Generic;
using UnityEngine;

namespace Chronomancers.Sim.Runtime
{
    /// <summary>
    /// The clock, and a span inspector.
    /// <para>
    /// Drawn with <c>OnGUI</c> so a playtest scene needs no canvas, no fonts and no prefab wiring.
    /// </para>
    /// <para>
    /// The span strip is the part worth having. Almost every bug in a system like this is a span with the
    /// wrong range, the wrong direction or the wrong precedence, and none of those are visible by watching
    /// the game — but all of them are obvious as coloured bars against a cursor line.
    /// </para>
    /// </summary>
    public sealed class SimHud : MonoBehaviour
    {
        [SerializeField] SimRunner runner;
        [SerializeField] int maxRows = 24;
        [SerializeField] bool showSpans = true;

        readonly List<BodyTimeline> _rows = new();
        GUIStyle _label;
        GUIStyle _small;

        void Awake()
        {
            if (runner == null) runner = FindAnyObjectByType<SimRunner>();
        }

        void OnGUI()
        {
            if (runner == null || runner.Timeline == null) return;

            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 16, richText = false };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 11, richText = false };

            var clock = runner.Clock;
            var direction = runner.Paused ? "paused" : clock.Dir > 0 ? "forward" : "backward";

            GUI.Label(new Rect(12f, 8f, 900f, 22f),
                $"loop {clock.Cursor.SecondsF:0.000} / {clock.Length.SecondsF:0.000} s" +
                $"   (raw {clock.Cursor.Raw})   rate {clock.Rate:0.00}  {direction}", _label);

            GUI.Label(new Rect(12f, 30f, 900f, 22f),
                $"control {Name(runner.Controlled)}   watch {Name(runner.Watched)}" +
                $"   live {runner.Live.Count}  recording {runner.OpenSpanCount}" +
                $"   bodies {runner.Timeline.BodyCount}  seq {runner.Timeline.CurrentSeq}" +
                $"   undo {runner.UndoDepth}", _label);

            GUI.Label(new Rect(12f, 52f, 900f, 22f),
                "WASD move   mouse aim   LMB/J fire   E interact   1-9 take control   Tab watch next   " +
                "Space pause   R rewind   Shift fast   Ctrl+Z undo   F5/F9 save/load", _small);

            if (showSpans) DrawSpans(new Rect(12f, 78f, Mathf.Min(Screen.width - 24f, 900f), 0f));
        }

        static string Name(SimBody body) => body != null ? body.name : "-";

        void DrawSpans(Rect area)
        {
            var timeline = runner.Timeline;
            var length = Mathf.Max(1, runner.Clock.Length.Raw);

            _rows.Clear();
            foreach (var body in timeline.Bodies) _rows.Add(body);
            _rows.Sort((a, b) => a.Body.Value.CompareTo(b.Body.Value));

            const float rowHeight = 14f;
            const float labelWidth = 120f;
            var barLeft = area.x + labelWidth;
            var barWidth = area.width - labelWidth;

            var y = area.y;
            var shown = 0;

            foreach (var body in _rows)
            {
                if (shown++ >= maxRows) break;

                var live = runner.Live.TryGetValue(body.Body, out var instance);
                var recording = runner.IsRecording(body.Body);

                GUI.color = recording ? new Color(1f, 0.9f, 0.4f) : live ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                GUI.Label(new Rect(area.x, y - 2f, labelWidth, rowHeight + 4f),
                    $"{(live ? instance.name : body.Body.ToString())}{(recording ? " *" : "")}", _small);

                // Track, so a body with no span anywhere is still visibly a row rather than absent.
                GUI.color = new Color(1f, 1f, 1f, 0.07f);
                GUI.DrawTexture(new Rect(barLeft, y, barWidth, rowHeight - 2f), Texture2D.whiteTexture);

                // Oldest first, so higher Seq draws on top — which is exactly the precedence rule, and
                // means what you see is what Resolve would return.
                for (var i = 0; i < body.SpanCount; i++)
                {
                    var span = body.GetSpan(i);
                    var x0 = barLeft + barWidth * Mathf.Clamp01(span.Min.Raw / (float)length);
                    var x1 = barLeft + barWidth * Mathf.Clamp01(span.Max.Raw / (float)length);

                    GUI.color = span.Kind == SpanKind.Void
                        ? new Color(0.85f, 0.2f, 0.2f, 0.9f)
                        : span.Dir > 0
                            ? new Color(0.3f, 0.75f, 0.35f, 0.9f)
                            : new Color(0.3f, 0.55f, 0.9f, 0.9f); // backward recordings read differently

                    GUI.DrawTexture(new Rect(x0, y, Mathf.Max(1f, x1 - x0), rowHeight - 2f),
                        Texture2D.whiteTexture);
                }

                y += rowHeight;
            }

            // The cursor, over everything.
            var cursorX = barLeft + barWidth * Mathf.Clamp01(runner.Cursor.Raw / (float)length);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cursorX, area.y - 2f, 1f, y - area.y + 4f), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(barLeft, y + 2f, 600f, 18f),
                "green forward   blue backward   red void   yellow name = recording", _small);

            GUI.color = Color.white;
        }
    }
}
