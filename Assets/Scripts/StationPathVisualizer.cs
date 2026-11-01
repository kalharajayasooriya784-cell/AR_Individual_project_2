using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// A token travels along a row of stations (phases / pipeline stages).
// Used for Waterfall, CI/CD and the Waterfall-vs-Agile comparison.
// Actions:  goto (value = station index), back (value = station index),
//           fail (value = station index), none
public class StationPathVisualizer : MonoBehaviour, IVisualizer
{
    [Header("Stations")]
    [SerializeField] GameObject stationPrefab;           // needs a child named "Pedestal"
    [SerializeField] string[] stationNames;
    [SerializeField, TextArea] string[] stationDescriptions;
    [SerializeField] float stepX = 0.15f;
    [SerializeField] float stepDown = 0.04f;              // 0 = flat row, >0 = waterfall staircase

    [Header("Token")]
    [SerializeField] Transform token;                     // child of this root
    [SerializeField] float tokenHeight = 0.1f;

    [Header("Colours")]
    [SerializeField] Color idleColor = new Color(0.69f, 0.75f, 0.78f);
    [SerializeField] Color activeColor = new Color(0.94f, 0.42f, 0.00f);
    [SerializeField] Color doneColor = new Color(0.00f, 0.54f, 0.48f);
    [SerializeField] Color failColor = new Color(0.56f, 0.14f, 0.67f);

    class StationView
    {
        public Transform root;
        public Renderer pedestal;
    }

    readonly List<StationView> stations = new List<StationView>();
    bool built;
    int current = -1;
    bool failed;
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
        current = -1;
        failed = false;
        SetProgress(-1);
        if (token != null) token.localPosition = TokenPos(-1);
    }

    public void Apply(LessonStep step)
    {
        if (step == null) return;
        Build();
        switch ((step.action ?? "").ToLowerInvariant())
        {
            case "goto":
            case "back": MoveTo(step.value); break;
            case "fail": Fail(step.value); break;
        }
    }

    // ---------- Free-play buttons (wire in Inspector) ----------

    public void AdvanceToken()
    {
        if (failed)                       // a failed stage stops the pipeline: fix, commit again, re-run
        {
            MoveTo(0);
            return;
        }
        MoveTo(Mathf.Min(current + 1, stations.Count - 1));
    }

    public void FailCurrent()             // make the current stage fail (CI/CD scene)
    {
        if (current >= 0) Fail(current);
    }

    public void ChangeRequest()
    {
        if (current > 0) MoveTo(0);   // go back to the first phase
    }

    // ---------- Internals ----------

    void Build()
    {
        if (built) return;
        built = true;

        for (int i = 0; i < stationNames.Length; i++)
        {
            var go = Instantiate(stationPrefab, transform);
            go.transform.localPosition = SlotPos(i);
            go.transform.localRotation = Quaternion.identity;

            var label = go.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = stationNames[i];

            var info = go.GetComponentInChildren<Inspectable>();
            if (info != null)
            {
                info.title = stationNames[i];
                info.description = i < stationDescriptions.Length ? stationDescriptions[i] : "";
            }

            var pedestal = go.transform.Find("Pedestal");
            stations.Add(new StationView
            {
                root = go.transform,
                pedestal = pedestal != null ? pedestal.GetComponent<Renderer>() : null
            });
        }
    }

    Vector3 SlotPos(int i)
    {
        int n = Mathf.Max(stationNames.Length, 1);
        float width = (n - 1) * stepX;
        float drop = (n - 1) * stepDown;
        return new Vector3(-width * 0.5f + i * stepX, drop - i * stepDown, 0f);
    }

    Vector3 TokenPos(int i) => SlotPos(i) + Vector3.up * tokenHeight;

    void MoveTo(int index)
    {
        if (stations.Count == 0) return;
        index = Mathf.Clamp(index, 0, stations.Count - 1);
        current = index;
        failed = false;
        SetProgress(index);

        if (token != null)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(Glide(token, TokenPos(index)));
        }
    }

    void Fail(int index)
    {
        if (stations.Count == 0) return;
        MoveTo(index);
        index = Mathf.Clamp(index, 0, stations.Count - 1);
        if (stations[index].pedestal != null)
            stations[index].pedestal.material.color = failColor;
        failed = true;
    }

    void SetProgress(int active)
    {
        for (int i = 0; i < stations.Count; i++)
        {
            var s = stations[i];
            if (s.pedestal != null)
                s.pedestal.material.color = i < active ? doneColor : (i == active ? activeColor : idleColor);
            s.root.localScale = Vector3.one * (i == active ? 1.15f : 1f);
        }
    }

    IEnumerator Glide(Transform t, Vector3 to)
    {
        Vector3 from = t.localPosition;
        const float duration = 0.6f;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, time / duration);
            t.localPosition = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.05f);
            yield return null;
        }
        t.localPosition = to;
    }
}