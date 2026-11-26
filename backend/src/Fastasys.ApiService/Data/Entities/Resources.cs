namespace Fastasys.ApiService.Data.Entities;

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Description { get; set; } = string.Empty;

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
