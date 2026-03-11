using SNIF.Core.Enums;

namespace SNIF.Core.Entities
{
    public class SwipeAction : BaseEntity
    {
        public string SwiperPetId { get; set; } = null!;
        public virtual Pet SwiperPet { get; set; } = null!;

        public string TargetPetId { get; set; } = null!;
        public virtual Pet TargetPet { get; set; } = null!;

        public SwipeDirection Direction { get; set; }
    }
}
