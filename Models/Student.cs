namespace HostelManagementSystem.Models;

public class Student
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string EnrollmentNumber { get; set; } = "";
    public string Department { get; set; } = "";
    public string Course { get; set; } = "";
}