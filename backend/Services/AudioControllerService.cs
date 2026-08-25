
namespace backend.Services;

public class AudioControllerService
{
    private readonly PythonWorkerService _python;

    private string _mode = "system";

    public AudioControllerService(PythonWorkerService python)
    {
        _python = python;
    }

    public string CurrentMode => _mode;

    public async Task<bool> SwitchMode(string mode)
    {
        mode = mode.ToLower();

        if (mode != "system" && mode != "headphone")
            return false;

        if (_mode == mode)
            return true;

        Console.WriteLine($"Switching Audio Mode -> {mode}");

        _mode = mode;

        // Future:
        // Restart worker with correct startup argument.
        // Keeping stable engine untouched for now.

        await _python.Restart(mode);
        _mode = mode;
        return true;
    }
}