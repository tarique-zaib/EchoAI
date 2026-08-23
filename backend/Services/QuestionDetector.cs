namespace backend.Services;

public static class QuestionDetector
{
    private static readonly string[] QuestionStarts =
    {
        "tell me",
        "why",
        "what",
        "how",
        "can you",
        "could you",
        "describe",
        "explain",
        "walk me through",
        "give me an example",
        "when did",
        "where did",
        "who",
        "which"
    };

    public static bool IsQuestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim().ToLower();

        if (text.EndsWith("?"))
            return true;

        return QuestionStarts.Any(text.StartsWith);
    }
}