using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Two lanes (Waterfall and Agile) share one time axis (weeks).
// Actions:
//   add   param = items separated by ';'
//            S|lane|label|start|end            a block on a lane   (lane = W or A)
//            S|lane|label|start|end|rework     a block shown in the "rework" colour
//            S|lane|label|start|end|change     a block shown in the "change" colour
//            R|lane|week                       a release marker
//            XR|lane|week                      remove a release marker
//            X|lane|label                      remove blocks with this label
//            C|week                            customer change-request marker
//   now   value = week the timeline moves to
public class TimelineCompareVisualizer : MonoBehaviour, IVisualizer
{
    [Header("Prefabs")]
    [SerializeField] GameObject segmentPrefab;   // children: "Body" (with Inspectable), "Label"
    [SerializeField] GameObject markerPrefab;    // children: "Body" (with Inspectable), "Label"
    [SerializeField] GameObject tickPrefab;      // a 3D TextMeshPro text

    [Header("Timeline")]
    [SerializeField] int maxWeek = 18;
    [SerializeField] float weekWidth = 0.05f;
    [SerializeField] float laneZ = 0.14f;        // Waterfall at +laneZ, Agile at -laneZ
    [SerializeField] float laneDepth = 0.16f;
    [SerializeField] float segmentGap = 0.004f;
    [SerializeField] int tickEvery = 2;
    [SerializeField] string waterfallName = "WATERFALL";
    [SerializeField] string agileName = "AGILE";

    [Header("Now line and status")]
    [SerializeField] Transform nowLine;          // thin cube spanning both lanes (child of this root)
    [SerializeField] TMP_Text nowLabel;          // optional (child of this root, NOT of the line)
    [SerializeField] TMP_Text statusText;        // optional

    [Header("Colours")]
    [SerializeField] Color idleColor = new Color(0.69f, 0.75f, 0.78f);
    [SerializeField] Color activeColor = new Color(0.94f, 0.42f, 0.00f);
    [SerializeField] Color doneColor = new Color(0.00f, 0.54f, 0.48f);
    [SerializeField] Color reworkColor = new Color(0.56f, 0.14f, 0.67f);
    [SerializeField] Color changeColor = new Color(0.12f, 0.53f, 0.90f);
    [SerializeField] Color releaseColor = new Color(0.99f, 0.85f, 0.21f);
    [SerializeField] Color ghostColor = new Color(0.60f, 0.60f, 0.60f);
    [SerializeField] Color changeFlagColor = new Color(0.85f, 0.11f, 0.38f);
    [SerializeField] Color waterfallTitleColor = new Color(0.56f, 0.79f, 0.98f);
    [SerializeField] Color agileTitleColor = new Color(1.00f, 0.80f, 0.50f);

    enum Kind { Normal, Rework, Change }

    class Seg
    {
        public string lane, label;
        public float start, end;
        public Kind kind;
        public GameObject go;
        public Transform body;
        public Renderer rend;
    }

    class Marker
    {
        public string lane;
        public int week;
        public bool isChange;
        public GameObject go;
        public Renderer rend;
    }

    readonly List<Seg> segs = new List<Seg>();
    readonly List<Marker> markers = new List<Marker>();
    bool built;
    int targetNow;
    float shownNow;
    Coroutine moveRoutine;

    void OnEnable()
    {
        Build();
        ResetState();
    }

    // ---------- IVisualizer ----------

    public void ResetState()
    {
        StopAllCoroutines();
        moveRoutine = null;
        Build();

        foreach (var s in segs) Destroy(s.go);
        foreach (var m in markers) Destroy(m.go);
        segs.Clear();
        markers.Clear();

        targetNow = 0;
        shownNow = 0f;
        UpdateVisuals(0f);
    }

    public void Apply(LessonStep step)
    {
        if (step == null) return;
        Build();

        switch ((step.action ?? "").ToLowerInvariant())
        {
            case "add": AddItems(step.param); break;
            case "now": SetNow(step.value); break;
        }
        UpdateVisuals(shownNow);
    }

    // ---------- Free-play (wire two buttons in the Inspector) ----------

    public void NextWeek() => SetNow(targetNow + 1);
    public void PrevWeek() => SetNow(targetNow - 1);

    // ---------- Build ----------

    void Build()
    {
        if (built) return;
        built = true;

        for (int w = 0; w <= maxWeek; w += Mathf.Max(tickEvery, 1))
        {
            var t = Instantiate(tickPrefab, transform);
            t.transform.localPosition = new Vector3(AxisX(w), 0.025f, 0f);
            var tmp = t.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = w.ToString();
        }

        MakeTitle("W", waterfallName, waterfallTitleColor);
        MakeTitle("A", agileName, agileTitleColor);
    }

    void MakeTitle(string lane, string text, Color color)
    {
        var t = Instantiate(tickPrefab, transform);
        t.transform.localPosition = new Vector3(AxisX(0) - 0.1f, 0.05f, LaneZ(lane));
        t.transform.localScale = Vector3.one * 1.6f;
        var tmp = t.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = color;
        }
    }

    float AxisX(float week) => -maxWeek * weekWidth * 0.5f + week * weekWidth;

    bool IsWaterfall(string lane) => lane.Trim().ToUpperInvariant().StartsWith("W");

    float LaneZ(string lane) => IsWaterfall(lane) ? laneZ : -laneZ;

    string LaneName(string lane) => IsWaterfall(lane) ? "Waterfall" : "Agile";

    // ---------- Items ----------

    void AddItems(string param)
    {
        if (string.IsNullOrEmpty(param)) return;

        foreach (var raw in param.Split(';'))
        {
            var p = raw.Split('|');
            if (p.Length < 2) continue;

            switch (p[0].Trim().ToUpperInvariant())
            {
                case "S":
                    {
                        if (p.Length < 5) break;
                        if (!int.TryParse(p[3].Trim(), out int start)) break;
                        if (!int.TryParse(p[4].Trim(), out int end)) break;
                        string kindText = p.Length > 5 ? p[5].Trim().ToLowerInvariant() : "";
                        Kind kind = kindText == "rework" ? Kind.Rework
                                  : kindText == "change" ? Kind.Change : Kind.Normal;
                        AddSegment(p[1].Trim(), p[2].Trim(), start, end, kind);
                        break;
                    }
                case "R":
                    {
                        if (p.Length < 3) break;
                        if (int.TryParse(p[2].Trim(), out int week)) AddMarker(p[1].Trim(), week, false);
                        break;
                    }
                case "XR":
                    {
                        if (p.Length < 3) break;
                        if (int.TryParse(p[2].Trim(), out int week)) RemoveMarker(p[1].Trim(), week);
                        break;
                    }
                case "X":
                    {
                        if (p.Length < 3) break;
                        RemoveSegments(p[1].Trim(), p[2].Trim());
                        break;
                    }
                case "C":
                    {
                        if (int.TryParse(p[1].Trim(), out int week)) AddMarker("W", week, true);
                        break;
                    }
            }
        }
    }

    void AddSegment(string lane, string label, int start, int end, Kind kind)
    {
        var go = Instantiate(segmentPrefab, transform);
        float x0 = AxisX(start);
        float x1 = AxisX(end);
        float length = Mathf.Max(x1 - x0 - segmentGap, 0.01f);

        go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, 0f, LaneZ(lane));
        go.transform.localRotation = Quaternion.identity;

        var body = go.transform.Find("Body");
        if (body != null)
        {
            body.localScale = new Vector3(length, 0.03f, laneDepth);
            body.localPosition = new Vector3(0f, 0.015f, 0f);
        }

        var text = go.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = label;
            text.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        }

        var info = go.GetComponentInChildren<Inspectable>();
        if (info != null)
        {
            info.title = label;
            string extra = kind == Kind.Rework ? " This is rework caused by the change request."
                         : kind == Kind.Change ? " This block includes the customer's change." : "";
            info.description = LaneName(lane) + ": from week " + start + " to week " + end + "." + extra;
        }

        segs.Add(new Seg
        {
            lane = lane,
            label = label,
            start = start,
            end = end,
            kind = kind,
            go = go,
            body = body,
            rend = body != null ? body.GetComponent<Renderer>() : null
        });
    }

    void RemoveSegments(string lane, string label)
    {
        for (int i = segs.Count - 1; i >= 0; i--)
        {
            if (IsWaterfall(segs[i].lane) == IsWaterfall(lane) && segs[i].label == label)
            {
                Destroy(segs[i].go);
                segs.RemoveAt(i);
            }
        }
    }

    void AddMarker(string lane, int week, bool isChange)
    {
        var go = Instantiate(markerPrefab, transform);
        float z = isChange ? 0f : LaneZ(lane);
        float y = isChange ? 0.2f : 0.14f;
        go.transform.localPosition = new Vector3(AxisX(week), y, z);
        go.transform.localRotation = Quaternion.identity;

        var text = go.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = (isChange ? "Change" : "Release") + "\nWk " + week;

        var info = go.GetComponentInChildren<Inspectable>();
        if (info != null)
        {
            info.title = isChange ? "Change request" : "Release";
            info.description = isChange
                ? "In week " + week + " the customer changes a requirement."
                : LaneName(lane) + ": working software reaches the customer in week " + week + ".";
        }

        var body = go.transform.Find("Body");
        markers.Add(new Marker
        {
            lane = lane,
            week = week,
            isChange = isChange,
            go = go,
            rend = body != null ? body.GetComponent<Renderer>() : null
        });
    }

    void RemoveMarker(string lane, int week)
    {
        for (int i = markers.Count - 1; i >= 0; i--)
        {
            var m = markers[i];
            if (!m.isChange && m.week == week && IsWaterfall(m.lane) == IsWaterfall(lane))
            {
                Destroy(m.go);
                markers.RemoveAt(i);
            }
        }
    }

    // ---------- Time ----------

    void SetNow(int week)
    {
        targetNow = Mathf.Clamp(week, 0, maxWeek);
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(AnimateNow(targetNow));
    }

    IEnumerator AnimateNow(float to)
    {
        float from = shownNow;
        const float duration = 1.0f;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, time / duration);
            shownNow = Mathf.Lerp(from, to, k);
            UpdateVisuals(shownNow);
            yield return null;
        }
        shownNow = to;
        UpdateVisuals(shownNow);
        moveRoutine = null;
    }

    void UpdateVisuals(float now)
    {
        foreach (var s in segs)
        {
            bool done = now >= s.end - 0.001f;
            bool active = !done && now > s.start + 0.001f;

            Color c;
            switch (s.kind)
            {
                case Kind.Rework: c = reworkColor; break;
                case Kind.Change: c = changeColor; break;
                default: c = done ? doneColor : (active ? activeColor : idleColor); break;
            }
            if (s.rend != null) s.rend.material.color = c;

            if (s.body != null)
            {
                float h = active ? 0.055f : 0.03f;
                Vector3 sc = s.body.localScale;
                s.body.localScale = new Vector3(sc.x, h, sc.z);
                s.body.localPosition = new Vector3(0f, h * 0.5f, 0f);
            }
        }

        int waterfallReleases = 0, agileReleases = 0;
        foreach (var m in markers)
        {
            bool reached = now + 0.001f >= m.week;
            if (m.isChange)
            {
                if (m.rend != null) m.rend.material.color = changeFlagColor;
                m.go.transform.localScale = Vector3.one * 1.2f;
                continue;
            }

            if (m.rend != null) m.rend.material.color = reached ? releaseColor : ghostColor;
            m.go.transform.localScale = Vector3.one * (reached ? 1.4f : 1f);

            if (reached)
            {
                if (IsWaterfall(m.lane)) waterfallReleases++;
                else agileReleases++;
            }
        }

        float x = AxisX(now);
        if (nowLine != null)
        {
            Vector3 p = nowLine.localPosition;
            nowLine.localPosition = new Vector3(x, p.y, p.z);
        }
        int week = Mathf.RoundToInt(now);
        if (nowLabel != null)
        {
            nowLabel.text = "Week " + week;
            nowLabel.transform.localPosition = new Vector3(x, 0.12f, 0f);
        }
        if (statusText != null)
            statusText.text = "Week " + week + "  |  Waterfall " + waterfallReleases +
                              "  |  Agile " + agileReleases;
    }
}
