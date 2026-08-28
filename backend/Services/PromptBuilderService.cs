using System.Text;
using System.Text.RegularExpressions;
using backend.Models;

namespace backend.Services;

public class PromptBuilderService
{
    public string Build(string question, ResumeProfile? profile, string mode = "quick")
    {
        if (profile == null)
            return BuildGeneric(question, mode);

        var keywords = ExtractKeywords(question);

        var relevantJobs = profile.Experience
            .Select(job => new
            {
                Job = job,
                Score = ScoreJob(job, keywords)
            })
            .OrderByDescending(x => x.Score)
            .Take(2)
            .Select(x => x.Job)
            .ToList();

        if (!relevantJobs.Any())
            relevantJobs = profile.Experience.Take(1).ToList();

        var relevantSkills = profile.Skills
            .Where(skill =>
                keywords.Any(k =>
                    skill.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Take(10)
            .ToList();

        var modeInstruction = mode.ToLower() switch
        {
            "quick" =>
                "Give a natural 20–30 second answer in 2–4 spoken sentences.",

            "detailed" =>
                "Give a detailed explanation with production context, examples, and best practices.",

            "interview" =>
                "Answer exactly as if speaking in a live technical interview. Include a concise answer, one practical example, and one likely follow-up interview question.",

            _ =>
                "Answer naturally."
        };

        var sb = new StringBuilder();

        sb.AppendLine("You are answering a LIVE software engineering interview as the candidate.");
        sb.AppendLine();
        sb.AppendLine($"ANSWER MODE: {mode.ToUpper()}");
        sb.AppendLine(modeInstruction);
        sb.AppendLine();

        sb.AppendLine("Grounding Rules:");
        sb.AppendLine("- Speak in first person as the candidate.");
        sb.AppendLine("- Use only the resume evidence below.");
        sb.AppendLine("- If the resume doesn't support a claim, explain the concept instead of inventing experience.");
        sb.AppendLine();

        sb.AppendLine("Candidate Summary:");
        sb.AppendLine(profile.Name);
        sb.AppendLine($"{profile.ExperienceYears}+ years experience");
        sb.AppendLine();

        sb.AppendLine("Most Relevant Experience:");

        var job = relevantJobs.First();

        sb.AppendLine("Selected Experience:");
        sb.AppendLine($"Role: {job.Title}");
        sb.AppendLine($"Company: {job.Company}");
        sb.AppendLine($"Duration: {job.Duration}");

        if (!string.IsNullOrWhiteSpace(job.Description))
        {
            var lines = job.Description
    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
    .Take(3);

            sb.AppendLine("Relevant Details:");
            foreach (var line in lines)
                sb.AppendLine(line);
        }

        if (job.Projects.Any() && keywords.Any(k =>
    job.Projects.Any(p => p.Contains(k, StringComparison.OrdinalIgnoreCase))))
        {
            sb.AppendLine("Relevant Project:");
            sb.AppendLine($"- {job.Projects.First()}");
        }

        sb.AppendLine();

        if (relevantSkills.Any())
        {
            sb.AppendLine("Relevant Skills:");
            sb.AppendLine(string.Join(", ", relevantSkills.Take(5)));
        }

        sb.AppendLine("Interview Question:");
        sb.AppendLine(question);
        sb.AppendLine();

        return sb.ToString();
    }

    private static List<string> ExtractKeywords(string question)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "what","how","why","when","where","which","who","can","could",
            "would","should","is","are","the","a","an","in","on","of","for",
            "to","and","or","with","about","explain","tell","me","difference",
            "between","does","work","works","using"
        };

        return Regex.Matches(question, @"[A-Za-z0-9#\.\+\-]+")
            .Select(m => m.Value)
            .Where(w => w.Length > 1 && !stopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ScoreJob(ExperienceItem job, List<string> keywords)
    {
        int score = 0;

        foreach (var k in keywords)
        {
            if (job.Title.Contains(k, StringComparison.OrdinalIgnoreCase))
                score += 5;

            if (job.Company.Contains(k, StringComparison.OrdinalIgnoreCase))
                score += 4;

            if (job.Description.Contains(k, StringComparison.OrdinalIgnoreCase))
                score += 3;

            if (job.Projects.Any(p =>
                p.Contains(k, StringComparison.OrdinalIgnoreCase)))
                score += 6;
        }

        return score;
    }

    private static string BuildGeneric(string question, string mode)
    {
        var modeInstruction = mode.ToLower() switch
        {
            "quick" => "Answer naturally in under 30 seconds.",
            "detailed" => "Give a detailed explanation with examples.",
            "interview" => "Answer exactly as if speaking in a live technical interview.",
            _ => "Answer naturally."
        };

        return $"""
You are answering a live software engineering interview.

Answer Mode:
{modeInstruction}

Question:
{question}

Give the answer according to the selected Answer Mode.
""";
    }
}