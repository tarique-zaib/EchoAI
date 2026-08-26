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

        var relevantProjects = profile.Projects
            .Where(project =>
                keywords.Any(k =>
                    project.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Take(3)
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

        sb.AppendLine("STRICT RULES:");
        sb.AppendLine("- You are the candidate.");
        sb.AppendLine("- Use ONLY the evidence shown below.");
        sb.AppendLine("- Do NOT infer technologies that are not explicitly mentioned.");
        sb.AppendLine("- If the selected experience does not mention the asked technology, do NOT claim you implemented it there.");
        sb.AppendLine("- Instead say: 'I'll explain the concept technically, and in my resume my closest related experience is shown below.'");
        sb.AppendLine("- Never combine details from different jobs.");
        sb.AppendLine("- Speak naturally in first person.");
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
            sb.AppendLine("Relevant Details:");
            sb.AppendLine(job.Description);
        }

        if (job.Projects.Any())
        {
            sb.AppendLine("Projects in this role:");
            foreach (var p in job.Projects)
                sb.AppendLine($"- {p}");
        }

        sb.AppendLine();

        if (relevantSkills.Any())
        {
            sb.AppendLine("Relevant Skills:");
            sb.AppendLine(string.Join(", ", relevantSkills));
            sb.AppendLine();
        }

        sb.AppendLine("Interview Question:");
        sb.AppendLine(question);
        sb.AppendLine();

        sb.AppendLine("Answer Format:");

        switch (mode.ToLower())
        {
            case "quick":
                sb.AppendLine("1. Give a natural 20–30 second answer.");
                sb.AppendLine("2. Reference the selected role only if the evidence explicitly supports it.");
                sb.AppendLine("3. End with one production example.");
                break;

            case "detailed":
                sb.AppendLine("1. Start with a concise answer.");
                sb.AppendLine("2. Explain how it works.");
                sb.AppendLine("3. Mention production considerations.");
                sb.AppendLine("4. Give one practical example.");
                sb.AppendLine("5. Mention one common pitfall.");
                break;

            case "interview":
                sb.AppendLine("1. Give a confident interview-ready answer.");
                sb.AppendLine("2. Reference the selected role only if supported by evidence.");
                sb.AppendLine("3. Give one practical example.");
                sb.AppendLine("4. Finish with one likely follow-up interview question.");
                break;
        }

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