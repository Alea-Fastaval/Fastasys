using System.ComponentModel.DataAnnotations.Schema;

namespace Fastasys.ApiService.Data.Entities;

[Table("deltagere")]
public class Participant
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public string Address { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string MedicalInfo { get; set; } = string.Empty;
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ParticipantSchedule> Schedules { get; set; } = new List<ParticipantSchedule>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<ParticipantFood> Foods { get; set; } = new List<ParticipantFood>();
    public virtual ICollection<ParticipantWear> Wears { get; set; } = new List<ParticipantWear>();

    public string DisplayName()
    {
        if (Nickname == string.Empty)
        {
            return LegalName();
        }
        else
        {
            return Nickname;
        }
    }

    public string LegalName()
    {
        return FirstName + " " + LastName;
    }
}
