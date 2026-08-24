namespace backend.Models;

public class ResumeProfile
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Headline { get; set; } = "";
    public string ExperienceYears { get; set; } = "";

    public List<string> Skills { get; set; } = [];
    public List<ExperienceItem> Experience { get; set; } = [];
    public List<string> Projects { get; set; } = [];
    public List<string> Education { get; set; } = [];
}

public class ExperienceItem
{
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Duration { get; set; } = "";

    // NEW
    public string Description { get; set; } = "";
    public List<string> Projects { get; set; } = [];
}