using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LessonUI : MonoBehaviour
{
    [SerializeField] StepController controller;
    [SerializeField] TMP_Text codeText;
    [SerializeField] TMP_Text explanationText;
    [SerializeField] TMP_Text stepCounterText;
    [SerializeField] Button nextButton;
    [SerializeField] Button previousButton;
    [SerializeField] Button resetButton;
    [SerializeField] AudioSource audioSource;   // optional

    void OnEnable()
    {
        controller.OnStepChanged += HandleStepChanged;
        nextButton.onClick.AddListener(controller.Next);
        previousButton.onClick.AddListener(controller.Previous);
        resetButton.onClick.AddListener(controller.ResetLesson);

        int idx = controller.CurrentIndex;
        HandleStepChanged(idx, idx >= 0 ? controller.Lesson.steps[idx] : null);
    }

    void OnDisable()
    {
        controller.OnStepChanged -= HandleStepChanged;
        nextButton.onClick.RemoveListener(controller.Next);
        previousButton.onClick.RemoveListener(controller.Previous);
        resetButton.onClick.RemoveListener(controller.ResetLesson);
    }

    void HandleStepChanged(int index, LessonStep step)
    {
        RenderCode(step != null ? step.highlightLine : -1);
        explanationText.text = step != null ? step.explanation : "Press Next to begin.";
        stepCounterText.text = $"Step {index + 1} / {controller.StepCount}";
        previousButton.interactable = index >= 0;
        nextButton.interactable = index + 1 < controller.StepCount;

        if (audioSource != null)
        {
            audioSource.Stop();
            if (step != null && step.narration != null)
            {
                audioSource.clip = step.narration;
                audioSource.Play();
            }
        }
    }

    void RenderCode(int activeLine)
    {
        var lines = controller.Lesson.codeLines;
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            // <noparse> so characters like < > in code (Stack<int>) are shown as text
            if (i == activeLine)
                sb.AppendLine($"<mark=#FFD54F66><noparse>{lines[i]}</noparse></mark>");
            else
                sb.AppendLine($"<noparse>{lines[i]}</noparse>");
        }
        codeText.text = sb.ToString();
    }
}
