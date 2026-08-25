using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/vision")]
public class VisionController : ControllerBase
{
    private readonly VisionService _visionService;

    public VisionController(VisionService visionService)
    {
        _visionService = visionService;
    }

    public class VisionRequest
    {
        public string ImagePath { get; set; } = "";
    }

    [HttpPost("explain")]
    public async Task<IActionResult> Explain([FromBody] VisionRequest request)
    {
        await _visionService.ExplainImage(request.ImagePath);

        return Ok();
    }
}