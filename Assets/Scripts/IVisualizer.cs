public interface IVisualizer
{
    void ResetState();            // clear the 3D scene
    void Apply(LessonStep step);  // perform one action (push, pop, ...)
}
