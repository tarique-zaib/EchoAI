using backend.Models;

namespace backend.Services;

public class ResumeMemoryService
{
    private ResumeProfile? _profile;

    public void Save(ResumeProfile profile)
    {
        _profile = profile;
    }

    public ResumeProfile? Get()
    {
        return _profile;
    }

    public bool HasProfile => _profile != null;

    public void Clear()
    {
        _profile = null;
    }
}