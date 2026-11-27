using System.ComponentModel.DataAnnotations.Schema;

namespace Fastasys.ApiService.Data.Entities;

[Table("activity_settings")]
public class ActivitySettings
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    public int SmallKarma { get; set; } = 7;
    public int MediumKarma { get; set; }
    public int BigKarma { get; set; } = 10;
}
