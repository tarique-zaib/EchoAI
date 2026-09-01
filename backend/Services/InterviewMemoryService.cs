public class InterviewMemoryService
{
    private readonly Queue<(string Question, string Answer)> _history = new();

    public void Add(string question, string answer)
    {
        _history.Enqueue((question, answer));

        while (_history.Count > 3)
            _history.Dequeue();
    }

    public IReadOnlyCollection<(string Question, string Answer)> GetHistory()
        => _history.ToList();

    public void Clear() => _history.Clear();
}