namespace SNIF.Core.Contracts
{
    public class PetMatchNotification
    {
        public string OwnerId { get; set; }
        public string MatchedPetId { get; set; }
        public string PetName { get; set; }
        public string Species { get; set; }
        public string Breed { get; set; }
        public double Distance { get; set; }
        public DateTime NotifiedAt { get; set; }
    }
}