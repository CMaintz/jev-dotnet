namespace Jev;

/// <summary>
/// One typed judgment returned by Jev, mirroring the <see cref="Question"/> that asked
/// for it: a <see cref="Choice"/> yields a <see cref="ChoiceAnswer"/>, a <see cref="Score"/>
/// a <see cref="ScoreAnswer"/>, and a <see cref="Noul"/> a <see cref="NoulAnswer"/>. Choice
/// and Score answers are also <see cref="CalibratedAnswer"/>s, carrying a confidence.
/// Pattern-match on it, or use the typed accessors on <see cref="SystemOneResponse"/>.
/// Answers compare by value, including the contents of their collections.
/// </summary>
public abstract record Answer
{
    private protected Answer()
    {
    }
}
