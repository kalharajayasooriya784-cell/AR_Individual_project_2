using UnityEngine;

[System.Serializable]
public class LessonStep
{
    [Tooltip("Index of the line to highlight in the list panel (first line = 0, -1 = none)")]
    public int highlightLine;

    [Tooltip("goto, back, fail or none")]
    public string action = "none";

    [Tooltip("Station / item number the action refers to (first = 0)")]
    public int value;

    [Tooltip("Extra text data for some actions (not needed for the Waterfall scene)")]
    public string param;

    [TextArea] public string explanation;

    public AudioClip narration;   // optional
}

[CreateAssetMenu(fileName = "NewLesson", menuName = "SprintAR/Lesson")]
public class Lesson : ScriptableObject
{
    public string title;
    public string linkedModule;

    [TextArea(1, 3)] public string[] codeLines;   // the list shown in the panel (phases, steps ...)
    public LessonStep[] steps;
}
