using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// Board with columns and cards. Used for Scrum (sprint + burndown) and Kanban (WIP limits).
// Actions:
//   addcard    param "id|title|points;id|title"   value = column     (points are optional)
//   move       param "S1>1;S2>2"                   (id>column)
//   trymove    like move, but reports a blocked card (WIP limit) to the learner
//   day        param = optional moves, then adds a burndown bar (Scrum)
//   focus      value = column to highlight (-1 = none)
//   newsprint  param = optional moves (Done cards leave, burndown restarts)
public class BoardVisualizer : MonoBehaviour, IVisualizer
{
    [Header("Prefabs")]
    [SerializeField] GameObject columnPrefab;   // children: "Tray" (with Inspectable), "Title"
    [SerializeField] GameObject cardPrefab;     // children: "Body" (with Inspectable), "Label"
    [SerializeField] GameObject barPrefab;      // Scrum only. children: "Body", "Label"

    [Header("Board")]
    [SerializeField] string[] columnNames = { "Product Backlog", "Sprint Backlog", "In Progress", "Done" };
    [SerializeField, TextArea] string[] columnDescriptions;
    [SerializeField] float columnSpacing = 0.24f;
    [SerializeField] float cardSpacing = 0.09f;
    [SerializeField] int maxCardsPerColumn = 5;
    [SerializeField] bool showPoints = true;

    [Header("Scrum")]
    [SerializeField] int sprintCapacity = 10;
    [SerializeField] Vector3 chartOrigin = new Vector3(-0.3f, 0f, 0.45f);
    [SerializeField] float barSpacing = 0.07f;
    [SerializeField] float barHeightPerPoint = 0.012f;

    [Header("Kanban")]
    [SerializeField] bool kanbanMode = false;
    [SerializeField] int[] wipLimits;           // one number per column, 0 = no limit

    [Header("UI (optional)")]
    [SerializeField] TMP_Text statusText;

    [Header("Colours")]
    [SerializeField] Color trayIdle = new Color(0.93f, 0.94f, 0.95f);
    [SerializeField] Color trayFocus = new Color(1.00f, 0.88f, 0.51f);
    [SerializeField] Color trayBlocked = new Color(0.56f, 0.14f, 0.67f);
    [SerializeField]
    Color[] cardColors =
    {
        new Color(0.69f, 0.75f, 0.78f),
        new Color(0.39f, 0.71f, 0.96f),
        new Color(0.94f, 0.42f, 0.00f),
        new Color(0.00f, 0.54f, 0.48f)
    };

    class Card
    {
        public string id, title;
        public int points, column;
        public GameObject go;
        public TMP_Text label;
        public Renderer body;
        public Inspectable info;
    }

    readonly List<Card> cards = new List<Card>();
    readonly List<GameObject> bars = new List<GameObject>();
    readonly Dictionary<GameObject, Coroutine> slides = new Dictionary<GameObject, Coroutine>();
    List<Card>[] columns;
    Renderer[] trays;
    bool built;
    Coroutine statusRoutine;
    int focusCol = -1;
    int blockedColumn = -1;
    string pendingMessage;

    void OnEnable()
    {
        Build();
        ResetState();
    }

    // ---------- IVisualizer ----------

    public void ResetState()
    {
        StopAllCoroutines();
        statusRoutine = null;
        slides.Clear();
        Build();

        foreach (var c in cards) Destroy(c.go);
        cards.Clear();
        foreach (var col in columns) col.Clear();
        ClearBars();
        SetFocus(-1);
        RefreshStatus();
    }

    public void Apply(LessonStep step)
    {
        if (step == null) return;
        Build();

        blockedColumn = -1;
        pendingMessage = null;

        switch ((step.action ?? "").ToLowerInvariant())
        {
            case "addcard":
                AddCards(step.param, step.value);
                SetFocus(step.value);
                break;

            case "move":
                {
                    int t = MoveCards(step.param);
                    if (t >= 0) SetFocus(t);
                    break;
                }

            case "trymove":
                {
                    int t = MoveCards(step.param);
                    if (t >= 0) SetFocus(t);
                    if (blockedColumn >= 0)
                    {
                        pendingMessage = WipMessage(blockedColumn);
                        StartCoroutine(FlashTray(blockedColumn));
                    }
                    break;
                }

            case "day":
                {
                    int t = MoveCards(step.param);
                    if (t >= 0) SetFocus(t);
                    AddBar();
                    break;
                }

            case "focus":
                SetFocus(step.value);
                break;

            case "newsprint":
                NewSprint(step.param);
                break;
        }

        RefreshStatus();
        if (pendingMessage != null) ShowStatus(pendingMessage);
    }

    // ---------- Free-play (wire the two buttons in the Inspector) ----------

    public void MoveSelectedForward() => MoveSelected(+1);
    public void MoveSelectedBack() => MoveSelected(-1);

    void MoveSelected(int dir)
    {
        var card = cards.Find(c => c.info != null && c.info == Inspectable.Selected);
        if (card == null)
        {
            ShowStatus("Tap a card first.");
            return;
        }

        int target = card.column + dir;
        if (target < 0 || target >= columns.Length) return;
        if (columns[target].Count >= maxCardsPerColumn) return;

        // Kanban rule: a column cannot hold more cards than its WIP limit
        if (WipFull(target))
        {
            ShowStatus(WipMessage(target));
            StartCoroutine(FlashTray(target));
            return;
        }

        // Scrum rule: a story entering the sprint must fit the team's capacity
        if (!kanbanMode && card.column == 0 && target == 1 &&
            SprintLoad() + card.points > sprintCapacity)
        {
            ShowStatus("Over capacity! Max " + sprintCapacity + " points per sprint.");
            return;
        }

        MoveCards(card.id + ">" + target);
        RefreshStatus();
    }

    // ---------- Build ----------

    void Build()
    {
        if (built) return;
        built = true;

        int n = columnNames.Length;
        columns = new List<Card>[n];
        trays = new Renderer[n];

        for (int c = 0; c < n; c++)
        {
            columns[c] = new List<Card>();

            var go = Instantiate(columnPrefab, transform);
            go.transform.localPosition = new Vector3(ColumnX(c), 0f, 0f);
            go.transform.localRotation = Quaternion.identity;

            int limit = WipLimit(c);
            var title = go.GetComponentInChildren<TMP_Text>();
            if (title != null)
                title.text = columnNames[c] + (limit > 0 ? "\n(WIP limit " + limit + ")" : "");

            var info = go.GetComponentInChildren<Inspectable>();
            if (info != null)
            {
                info.title = columnNames[c];
                info.description = c < columnDescriptions.Length ? columnDescriptions[c] : "";
            }

            var tray = go.transform.Find("Tray");
            trays[c] = tray != null ? tray.GetComponent<Renderer>() : null;
        }
    }

    float ColumnX(int c) => (c - (columnNames.Length - 1) * 0.5f) * columnSpacing;

    Vector3 SlotPos(int col, int index) =>
        new Vector3(ColumnX(col), 0.01f, 0.18f - index * cardSpacing);

    // ---------- WIP limits ----------

    int WipLimit(int col) =>
        (wipLimits != null && col < wipLimits.Length) ? wipLimits[col] : 0;

    bool WipFull(int col)
    {
        int limit = WipLimit(col);
        return limit > 0 && columns[col].Count >= limit;
    }

    string WipMessage(int col) =>
        "WIP limit reached for " + columnNames[col] + " (max " + WipLimit(col) + "). Finish work first.";

    // ---------- Cards ----------

    void AddCards(string param, int column)
    {
        if (string.IsNullOrEmpty(param)) return;
        column = Mathf.Clamp(column, 0, columns.Length - 1);

        foreach (var item in param.Split(';'))
        {
            var parts = item.Split('|');
            if (parts.Length < 2) continue;
            if (columns[column].Count >= maxCardsPerColumn) break;

            int points = 1;
            if (parts.Length >= 3) int.TryParse(parts[2].Trim(), out points);

            var go = Instantiate(cardPrefab, transform);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localPosition = SlotPos(column, columns[column].Count);

            var card = new Card
            {
                id = parts[0].Trim(),
                title = parts[1].Trim(),
                points = points,
                column = column,
                go = go,
                label = go.GetComponentInChildren<TMP_Text>(),
                info = go.GetComponentInChildren<Inspectable>()
            };
            var body = go.transform.Find("Body");
            card.body = body != null ? body.GetComponent<Renderer>() : null;

            cards.Add(card);
            columns[column].Add(card);
            Refresh(card);
            StartCoroutine(PopIn(go.transform));
        }
    }

    int MoveCards(string param)
    {
        if (string.IsNullOrEmpty(param)) return -1;

        int last = -1;
        var touched = new HashSet<int>();

        foreach (var item in param.Split(';'))
        {
            var parts = item.Split('>');
            if (parts.Length < 2) continue;
            if (!int.TryParse(parts[1].Trim(), out int target)) continue;
            target = Mathf.Clamp(target, 0, columns.Length - 1);

            string id = parts[0].Trim();
            var card = cards.Find(c => c.id == id);
            if (card == null || columns[target].Count >= maxCardsPerColumn) continue;

            // WIP limit: the card stays where it is
            if (card.column != target && WipFull(target))
            {
                blockedColumn = target;
                continue;
            }

            touched.Add(card.column);
            columns[card.column].Remove(card);
            card.column = target;
            columns[target].Add(card);
            touched.Add(target);

            Refresh(card);
            last = target;
        }

        foreach (int col in touched) Layout(col);
        return last;
    }

    void Layout(int col)
    {
        for (int i = 0; i < columns[col].Count; i++)
            Slide(columns[col][i].go, SlotPos(col, i));
    }

    void Slide(GameObject go, Vector3 target)
    {
        if (slides.TryGetValue(go, out var running) && running != null)
            StopCoroutine(running);
        slides[go] = StartCoroutine(SlideTo(go.transform, target));
    }

    void Refresh(Card card)
    {
        if (card.label != null)
            card.label.text = showPoints ? card.id + " (" + card.points + ")" : card.id;

        if (card.body != null)
            card.body.material.color = cardColors[Mathf.Min(card.column, cardColors.Length - 1)];

        if (card.info != null)
        {
            card.info.title = card.id + ": " + card.title;
            card.info.description =
                (showPoints ? "Story points: " + card.points + ". " : "") +
                "Status: " + columnNames[card.column] + ".";
        }
    }

    // ---------- Scrum: burndown ----------

    int SprintLoad()      // everything committed to the sprint (Sprint Backlog, In Progress, Done)
    {
        int sum = 0;
        foreach (var c in cards)
            if (c.column >= 1) sum += c.points;
        return sum;
    }

    int RemainingPoints() // work still to do (Sprint Backlog + In Progress)
    {
        int sum = 0;
        int lastCol = columns.Length - 1;
        foreach (var c in cards)
            if (c.column >= 1 && c.column < lastCol) sum += c.points;
        return sum;
    }

    void AddBar()
    {
        if (barPrefab == null) return;
        int remaining = RemainingPoints();

        var bar = Instantiate(barPrefab, transform);
        bar.transform.localPosition = chartOrigin + new Vector3(bars.Count * barSpacing, 0f, 0f);
        bar.transform.localRotation = Quaternion.identity;

        float h = Mathf.Max(remaining * barHeightPerPoint, 0.004f);

        var body = bar.transform.Find("Body");
        if (body != null)
        {
            Vector3 s = body.localScale;
            body.localScale = new Vector3(s.x, h, s.z);
            body.localPosition = new Vector3(0f, h * 0.5f, 0f);
        }

        var label = bar.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = "D" + (bars.Count + 1) + "\n" + remaining;
            label.transform.localPosition = new Vector3(0f, h + 0.035f, 0f);
        }

        bars.Add(bar);
        StartCoroutine(PopIn(bar.transform));
    }

    void ClearBars()
    {
        foreach (var b in bars) Destroy(b);
        bars.Clear();
    }

    void NewSprint(string param)
    {
        // Shipped (Done) stories leave the board and the burndown starts again
        int doneCol = columns.Length - 1;
        foreach (var card in columns[doneCol])
        {
            cards.Remove(card);
            slides.Remove(card.go);
            Destroy(card.go);
        }
        columns[doneCol].Clear();

        ClearBars();
        SetFocus(-1);

        int t = MoveCards(param);
        if (t >= 0) SetFocus(t);
    }

    // ---------- Focus / status ----------

    void SetFocus(int col)
    {
        focusCol = col;
        for (int i = 0; i < trays.Length; i++)
            if (trays[i] != null)
                trays[i].material.color = i == col ? trayFocus : trayIdle;
    }

    IEnumerator FlashTray(int col)
    {
        if (col < 0 || col >= trays.Length || trays[col] == null) yield break;
        trays[col].material.color = trayBlocked;
        yield return new WaitForSeconds(0.8f);
        SetFocus(focusCol);
    }

    void RefreshStatus()
    {
        if (statusText == null) return;
        if (statusRoutine != null)
        {
            StopCoroutine(statusRoutine);
            statusRoutine = null;
        }

        if (kanbanMode)
        {
            var sb = new StringBuilder("WIP  ");
            bool first = true;
            for (int c = 0; c < columns.Length; c++)
            {
                int limit = WipLimit(c);
                if (limit <= 0) continue;
                if (!first) sb.Append("  |  ");
                sb.Append(columnNames[c]).Append(' ').Append(columns[c].Count).Append('/').Append(limit);
                first = false;
            }
            statusText.text = sb.ToString();
        }
        else
        {
            statusText.text = "Sprint load: " + SprintLoad() + " / " + sprintCapacity + " points";
        }
    }

    void ShowStatus(string message)
    {
        if (statusText == null) return;
        statusText.text = message;
        if (statusRoutine != null) StopCoroutine(statusRoutine);
        statusRoutine = StartCoroutine(RestoreStatus());
    }

    IEnumerator RestoreStatus()
    {
        yield return new WaitForSeconds(3f);
        statusRoutine = null;
        RefreshStatus();
    }

    // ---------- Animations ----------

    IEnumerator PopIn(Transform t)
    {
        Vector3 target = t.localScale;
        const float duration = 0.25f;
        float time = 0f;
        while (time < duration)
        {
            if (t == null) yield break;
            time += Time.deltaTime;
            t.localScale = Vector3.Lerp(Vector3.zero, target, time / duration);
            yield return null;
        }
        if (t != null) t.localScale = target;
    }

    IEnumerator SlideTo(Transform t, Vector3 to)
    {
        Vector3 from = t.localPosition;
        const float duration = 0.5f;
        float time = 0f;
        while (time < duration)
        {
            if (t == null) yield break;
            time += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, time / duration);
            t.localPosition = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.04f);
            yield return null;
        }
        if (t != null) t.localPosition = to;
    }
}

