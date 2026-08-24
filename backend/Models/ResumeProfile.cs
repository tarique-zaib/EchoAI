namespace backend.Models;

public class ResumeProfile
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Headline { get; set; } = "";
    public string ExperienceYears { get; set; } = "";

    public List<string> Skills { get; set; } = new();
    public List<ExperienceItem> Experience { get; set; } = new();
    public List<string> Projects { get; set; } = new();
    public List<string> Education { get; set; } = new();
}

public class ExperienceItem
{
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Duration { get; set; } = "";
}