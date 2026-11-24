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

public class FoodType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameEnglish { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<ParticipantFood> ParticipantFoodOrders { get; set; } = new List<ParticipantFood>();
}

public class ParticipantFood
{
    public int ParticipantId { get; set; }
    public virtual Participant Participant { get; set; } = null!;
    public int FoodTypeId { get; set; }
    public virtual FoodType FoodType { get; set; } = null!;
    public DateTime Date { get; set; }
    public int Quantity { get; set; } = 1;
}

public class WearItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Size { get; set; } = string.Empty;
    public int Stock { get; set; }

    public virtual ICollection<ParticipantWear> ParticipantWearItems { get; set; } = new List<ParticipantWear>();
}

public class ParticipantWear
{
    public int ParticipantId { get; set; }
    public virtual Participant Participant { get; set; } = null!;
    public int WearItemId { get; set; }
    public virtual WearItem WearItem { get; set; } = null!;
    public int Quantity { get; set; } = 1;
}
