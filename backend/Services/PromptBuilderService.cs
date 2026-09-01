using System.Text;
using System.Text.RegularExpressions;
using backend.Models;

namespace backend.Services;

public class PromptBuilderService
{
    private readonly InterviewMemoryService _memory;

    public PromptBuilderService(InterviewMemoryService memory)
    {
        _memory = memory;
    }

    private static string DetectIntent(string question)
    {
        var q = question.ToLowerInvariant();

        if (q.Contains("tell me about yourself") || q.Contains("introduce yourself"))
            return "introduction";

        if (q.Contains("previous experience") || q.Contains("typical day"))
            return "experience";

        if (q.Contains("difference between"))
            return "comparison";

        if (q.StartsWith("what is") || q.StartsWith("explain"))
            return "concept";

        if (q.Contains("how did you") || q.Contains("tell me about a time"))
            return "behavioral";

        if (q.Contains("design") || q.Contains("architecture"))
            return "system";

        return "general";
    }
    public string Build(string question, ResumeProfile? profile, string mode = "quick")
    {
        if (profile == null)
            return BuildGeneric(question, mode);

        var keywords = ExtractKeywords(question);

        var relevantJobs = profile.Experience
            .Select(job => new { Job = job, Score = ScoreJob(job, keywords) })
            .OrderByDescending(x => x.Score)
            .Take(2)
            .Select(x => x.Job)
            .ToList();

        if (!relevantJobs.Any())
            relevantJobs = profile.Experience.Take(1).ToList();

        var relevantSkills = profile.Skills
            .Where(skill => keywords.Any(k =>
                skill.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Take(5)
            .ToList();

        var modeInstruction = mode.ToLower() switch
        {
            "quick" => """
You are answering ONE live interview question.

- Speak naturally in first person.
- Keep it around 20–30 seconds.
- Use 2–4 conversational sentences.
- Do not use headings.
""",

            "detailed" => """
You are answering ONE live interview question.

- Give a complete explanation.
- Include production context where relevant.
- Use examples naturally.
- Speak like you're explaining to an interviewer.
""",

            "interview" => """
You are answering ONE live interview question.

Rules:
- Answer ONLY the current interview question.
- Ignore previous answers unless this question explicitly refers to them.
- Speak in first person as the candidate.
- Give a complete, natural interview answer.
- Use resume experience only when relevant.
- Never invent projects or technologies.
- Never continue from previous responses.
- Never start with 'Sure', 'As I mentioned', or 'In my previous role'.
- Do not use markdown headings unless the interviewer asks.
""",

            _ => "Answer naturally."
        };
        var intent = DetectIntent(question);

        var intentInstruction = intent switch
        {
            "introduction" => """
Focus on career progression.
Mention relevant technologies naturally.
End with what you're working on today.
""",

            "experience" => """
Describe your responsibilities, technologies used,
and a typical workday conversationally.
""",

            "comparison" => """
Compare both technologies clearly.
Mention trade-offs and real-world usage.
""",

            "concept" => """
Explain the concept first,
then give one practical example.
""",

            "behavioral" => """
Answer using a natural STAR structure
without explicitly saying STAR.
""",

            "system" => """
Explain the approach,
trade-offs,
and production considerations.
""",

            _ => "Answer naturally."
        };

        var sb = new StringBuilder();
        sb.AppendLine("Answer Style:");
        sb.AppendLine(intentInstruction);
        sb.AppendLine();
        sb.AppendLine("You are answering a LIVE software engineering interview as the candidate.");
        sb.AppendLine();
        sb.AppendLine("================ INTERVIEW MODE ================");
        sb.AppendLine("This is a NEW interview question.");
        sb.AppendLine("Ignore previous answers.");
        sb.AppendLine("Answer only this question.");
        sb.AppendLine("===============================================");
        sb.AppendLine();

        sb.AppendLine($"ANSWER MODE: {mode.ToUpper()}");
        sb.AppendLine(modeInstruction);
        sb.AppendLine();

        sb.AppendLine("Grounding Rules:");
        sb.AppendLine("- Speak in first person.");
        sb.AppendLine("- Use resume evidence naturally.");
        sb.AppendLine("- Never invent experience.");
        sb.AppendLine("- If the resume doesn't support a claim, explain the concept honestly.");
        sb.AppendLine("- Mention technologies only when they belong to the relevant experience.");
        sb.AppendLine();

        sb.AppendLine("Technology Rules:");
        sb.AppendLine("- Don't list every technology from the resume.");
        sb.AppendLine("- Mention only technologies relevant to this question.");
        sb.AppendLine("- Don't force Azure, Angular, React Native, or .NET unless relevant.");
        sb.AppendLine();

        sb.AppendLine("Candidate Summary:");
        sb.AppendLine(profile.Name);
        sb.AppendLine($"{profile.ExperienceYears}+ years experience");
        sb.AppendLine();

        sb.AppendLine("Relevant Resume Evidence:");

        foreach (var job in relevantJobs)
        {
            sb.AppendLine($"Role: {job.Title}");
            sb.AppendLine($"Company: {job.Company}");
            sb.AppendLine($"Duration: {job.Duration}");

            if (!string.IsNullOrWhiteSpace(job.Description))
            {
                foreach (var line in job.Description
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2))
                {
                    sb.AppendLine($"- {line.Trim()}");
                }
            }

            if (job.Projects.Any())
            {
                var matchingProject = job.Projects.FirstOrDefault(p =>
                    keywords.Any(k => p.Contains(k, StringComparison.OrdinalIgnoreCase)));

                if (!string.IsNullOrWhiteSpace(matchingProject))
                {
                    sb.AppendLine($"Project: {matchingProject}");
                }
            }

            sb.AppendLine();
        }

        if (relevantSkills.Count >= 2)
        {
            sb.AppendLine("Relevant Skills:");
            sb.AppendLine(string.Join(", ", relevantSkills));
            sb.AppendLine();
        }

        var history = _memory.GetHistory();

        if (history.Any())
        {
            sb.AppendLine("Current Interview Context:");

            foreach (var item in history)
            {
                sb.AppendLine($"Interviewer: {item.Question}");
                sb.AppendLine($"Candidate: {item.Answer}");
                sb.AppendLine();
            }
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
                score += 8;

            if (job.Projects.Any(p =>
                p.Contains(k, StringComparison.OrdinalIgnoreCase)))
                score += 10;

            if (job.Description.Contains(k, StringComparison.OrdinalIgnoreCase))
                score += 5;

            if (job.Company.Contains(k, StringComparison.OrdinalIgnoreCase))
                score += 2;
        }

        return score;
    }
    private static string BuildGeneric(string question, string mode)
    {
        var modeInstruction = mode.ToLower() switch
        {
            "quick" => "Answer naturally in under 30 seconds.",
            "detailed" => "Give a detailed explanation with examples.",
            "interview" => "Answer naturally as if speaking to a live interviewer.",
            _ => "Answer naturally."
        };

        return $"""
You are answering ONE live software engineering interview question.

Rules:
- Speak naturally.
- Answer only this question.
- Don't continue previous responses.
- Speak in first person.
- Sound conversational rather than reading bullet points.
- Never invent experience.

Answer Mode:
{modeInstruction}

Question:
{question}
""";

    }
}