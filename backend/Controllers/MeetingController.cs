using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using System.Text;

namespace backend.Controllers;

[ApiController]
[Route("api/meeting")]
public class MeetingController : ControllerBase
{
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [HttpGet("detect")]
    public IActionResult Detect()
    {
        string detected = "";

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var sb = new StringBuilder(512);
            GetWindowText(hWnd, sb, sb.Capacity);

            var title = sb.ToString().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(title))
                return true;

            Console.WriteLine(title); // Debug

            if (title.Contains("microsoft teams"))
            {
                detected = "Microsoft Teams";
                return false;
            }

            if (title.Contains("google meet"))
            {
                detected = "Google Meet";
                return false;
            }

            if (title.Contains("zoom"))
            {
                detected = "Zoom";
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return Ok(new
        {
            detected = !string.IsNullOrEmpty(detected),
            app = detected
        });
    }

    [HttpGet("windows")]
    public IActionResult Windows()
    {
        var titles = new List<string>();

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var sb = new StringBuilder(512);
            GetWindowText(hWnd, sb, sb.Capacity);

            var title = sb.ToString().Trim();

            if (!string.IsNullOrWhiteSpace(title))
                titles.Add(title);

            return true;
        }, IntPtr.Zero);

        return Ok(titles);
    }
}