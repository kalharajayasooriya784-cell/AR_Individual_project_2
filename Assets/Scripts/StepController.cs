using System;
using UnityEngine;

public class StepController : MonoBehaviour
{
    [SerializeField] Lesson lesson;
    [SerializeField] MonoBehaviour visualizerBehaviour;   // must implement IVisualizer

    IVisualizer viz;
    int current = -1;

    public Lesson Lesson => lesson;
    public int CurrentIndex => current;
    public int StepCount => lesson != null ? lesson.steps.Length : 0;

    // UI listens to this: highlight code, show text, play audio
    public event Action<int, LessonStep> OnStepChanged;

    void Awake()
    {
        // Optional field: if left empty, find the visualizer on this same object
        viz = visualizerBehaviour as IVisualizer;
        if (viz == null) viz = GetComponent<IVisualizer>();
        if (viz == null) Debug.LogError("No IVisualizer found. Add Stack Visualizer to this object.", this);
    }

    public void Next()
    {
        if (current + 1 >= StepCount) return;
        current++;
        viz.Apply(lesson.steps[current]);
        Notify();
    }

    public void Previous()
    {
        if (current >= 0) GoTo(current - 1);
    }

    public void ResetLesson() => GoTo(-1);

    // Going back = clear everything and replay from the start
    public void GoTo(int target)
    {
        viz.ResetState();
        for (int i = 0; i <= target; i++) viz.Apply(lesson.steps[i]);
        current = target;
        Notify();
    }

    void Notify()
    {
        OnStepChanged?.Invoke(current, current >= 0 ? lesson.steps[current] : null);
    }
}
