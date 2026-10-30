using UnityEngine;

// Put this on any object the learner can tap to read details about it
public class Inspectable : MonoBehaviour
{
    public string title;
    [TextArea] public string description;
}
