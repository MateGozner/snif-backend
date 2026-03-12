namespace SNIF.Core.Enums
{
    public enum RejectReason
    {
        SelfPet,
        SameOwner,
        SpeciesMismatch,
        BreedMismatch,
        AgeBelowMin,
        AgeAboveMax,
        GenderPreference,
        BreedingGenderGuard,
        OutOfRange,
        ExistingMatch,
        LockedByEntitlement
    }
}
