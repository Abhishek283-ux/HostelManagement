
namespace HostelManagementSystem.Models;

public class Room
{
    public int Id { get; set; }
    public string RoomNumber { get; set; } = "";
    public string RoomType { get; set; } = "";
    public int Floor { get; set; }
    public int TotalBeds { get; set; }
}
