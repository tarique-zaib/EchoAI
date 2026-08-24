using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using backend.Models;

namespace backend.Services;

public class ResumeParserService
{
    private static readonly string[] SectionNames =
    {
        "SUMMARY",
        "PROFILE",
        "PROFESSIONAL SUMMARY",
        "TECHNICAL SKILLS",
        "SKILLS",
        "EXPERIENCE",
        "PROFESSIONAL EXPERIENCE",
        "PROFESSIONAL EXPERIENCE — CONTINUED",
        "CAREER HIGHLIGHTS",
        "PROJECTS",
        "EDUCATION",
        "CERTIFICATIONS",
        "ADDITIONAL INFORMATION"
    };

    public ResumeProfile Parse(string path)
    {
        var text = ExtractText(path);

        var sections = SplitSections(text);

        return new ResumeProfile
        {
            Name = ExtractName(text),
            Email = ExtractEmail(text),
            Phone = ExtractPhone(text),
            Headline = ExtractHeadline(text),
            ExperienceYears = ExtractExperienceYears(text),

            Skills = ExtractSkills(
                sections.GetValueOrDefault("TECHNICAL SKILLS") ??
                sections.GetValueOrDefault("SKILLS") ?? ""),

            Experience = ExtractExperienceItems(text),

            Projects = ExtractProjects(text),

            Education = ExtractEducation(text)
        };
    }

    // =====================================================
    // TEXT EXTRACTION
    // =====================================================

    private static string ExtractText(string path)
    {
        if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return ExtractPdf(path);

        if (path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return ExtractDocx(path);

        return File.ReadAllText(path);
    }

    private static string ExtractDocx(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);

        var sb = new StringBuilder();

        var body = doc.MainDocumentPart!.Document.Body!;

        int i = 0;

        foreach (var para in body.Descendants<Paragraph>())
        {
            var text = para.InnerText.Trim();

            if (!string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine($"{i++}: {text}");
                sb.AppendLine(text);
            }
        }

        File.WriteAllText(
            Path.Combine(
                Path.GetDirectoryName(path)!,
                "debug_docx_order.txt"),
            sb.ToString());

        return sb.ToString();
    }

    private static string ExtractPdf(string path)
    {
        using var pdf = PdfDocument.Open(path);

        var sb = new StringBuilder();

        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);

        return sb.ToString();
    }

    // =====================================================
    // SECTION SPLITTER
    // =====================================================

    private static Dictionary<string, string> SplitSections(string text)
    {
        var sections = new Dictionary<string, string>();
        var current = "HEADER";
        var sb = new StringBuilder();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (SectionNames.Any(s =>
                s.Equals(line, StringComparison.OrdinalIgnoreCase)))
            {
                sections[current] = sb.ToString().Trim();
                sb.Clear();
                current = line.ToUpperInvariant();
                continue;
            }

            sb.AppendLine(line);
        }

        sections[current] = sb.ToString().Trim();

        return sections;
    }

    // =====================================================
    // HEADER
    // =====================================================

    private static string ExtractName(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var value = line.Trim();

            if (Regex.IsMatch(value, @"^[A-Za-z][A-Za-z\s\.]+$"))
                return value;
        }

        return "";
    }

    private static string ExtractEmail(string text)
    {
        return Regex.Match(
            text,
            @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")
            .Value;
    }

    private static string ExtractPhone(string text)
    {
        return Regex.Match(
            text,
            @"(\+91[\s-]?)?[6-9]\d{9}")
            .Value;
    }

    private static string ExtractHeadline(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var value = line.Trim();

            if (value.Contains("Developer", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Engineer", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Architect", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Lead", StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return "";
    }

    private static string ExtractExperienceYears(string text)
    {
        var m = Regex.Match(
            text,
            @"(\d+)\+?\s+years",
            RegexOptions.IgnoreCase);

        return m.Success ? m.Groups[1].Value : "";
    }

    // =====================================================
    // SKILLS
    // =====================================================

    private static List<string> ExtractSkills(string text)
    {
        var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Front-End Technologies",
            "Back-End Technologies",
            "Cloud & DevOps",
            "Databases",
            "Architecture & Methodologies",
            "Tools & Platforms",
            "Testing Frameworks"
        };

        var cleaned = text
            .Replace("–", "-")
            .Replace("Framework (1.1-9.0)", "Framework 1.1-9.0");

        return cleaned
            .Split(new[] { ',', '|', '•', '\n' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 1)
            .Where(x => !ignore.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    // =====================================================
    // EXPERIENCE (Generic)
    // =====================================================

    private static List<ExperienceItem> ExtractExperienceItems(string text)
    {
        var jobs = new List<ExperienceItem>();

        var lines = text.Split('\n')
            .Select(l => Regex.Replace(l.Trim(), @"^-?\d+", "")) // remove page artifacts
            .Select(l => Regex.Replace(l, @"^\d+", ""))          // remove numeric prefixes
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var dateRegex = new Regex(
            @"^(.*)\|\s*(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec).*(Present|Current|20\d{2})$",
            RegexOptions.IgnoreCase);

        for (int i = 0; i < lines.Count; i++)
        {
            if (!dateRegex.IsMatch(lines[i]))
                continue;

            if (i == 0)
                continue;

            var title = lines[i - 1].Trim();
            var location = lines[i].Split('|')[0].Trim();
            var duration = lines[i].Split('|')[1].Trim();

            // Skip non-job lines
            if (title.StartsWith("Project:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (title.StartsWith("Technologies:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (title.Contains("PROFESSIONAL EXPERIENCE", StringComparison.OrdinalIgnoreCase))
                continue;

            jobs.Add(new ExperienceItem
            {
                Title = title,
                Company = location,
                Duration = duration
            });
        }

        return jobs
            .GroupBy(x => $"{x.Title}|{x.Company}")
            .Select(g => g.First())
            .ToList();
    }

    // =====================================================
    // PROJECTS
    // =====================================================

    private static List<string> ExtractProjects(string text)
    {
        var projects = new List<string>();

        foreach (Match m in Regex.Matches(
            text,
            @"Project:\s*(.+)",
            RegexOptions.IgnoreCase))
        {
            projects.Add(m.Groups[1].Value.Trim());
        }

        return projects.Distinct().ToList();
    }

    // =====================================================
    // EDUCATION
    // =====================================================

    private static List<string> ExtractEducation(string text)
    {
        var lines = text.Split('\n')
            .Select(x => x.Trim())
            .ToList();

        var start = lines.FindIndex(x =>
            x.Equals("EDUCATION", StringComparison.OrdinalIgnoreCase));

        if (start == -1)
            return new();

        var result = new List<string>();

        for (int i = start + 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                break;

            if (SectionNames.Any(s =>
                s.Equals(lines[i], StringComparison.OrdinalIgnoreCase)))
                break;

            result.Add(lines[i]);
        }

        return result.Distinct().ToList();
    }
}