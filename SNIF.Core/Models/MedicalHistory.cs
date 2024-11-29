namespace SNIF.Core.Models
{
    public class MedicalHistory : BaseModel
    {
        public MedicalHistory()
        {
            HealthIssues = new List<string>();
            VaccinationRecords = new List<string>();
        }

        public bool IsVaccinated { get; set; }
        public virtual ICollection<string> HealthIssues { get; set; }
        public virtual ICollection<string> VaccinationRecords { get; set; }
        public DateTime? LastCheckup { get; set; }
        public string? VetContact { get; set; }
    }
}