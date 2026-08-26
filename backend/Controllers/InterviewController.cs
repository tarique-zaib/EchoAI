using Microsoft.AspNetCore.Mvc;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/interview")]
public class InterviewController : ControllerBase
{
    private readonly PythonWorkerService _worker;

    public InterviewController(PythonWorkerService worker)
    {
        _worker = worker;
    }

    [HttpPost("start")]
    public IActionResult Start()
    {
        _worker.Start();
        return Ok();
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        _worker.Stop();
        return Ok();
    }
    [HttpPost("regenerate")]
    public async Task<IActionResult> Regenerate()
    {
        await _worker.RegenerateLastAnswer();
        return Ok();
    }
}