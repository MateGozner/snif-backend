
using Microsoft.AspNetCore.Http;
using SNIF.Core.DTOs;

namespace SNIF.Core.Interfaces
{
    public interface IPetService
    {
        Task<IEnumerable<PetDto>> GetUserPetsAsync(string userId);
        Task<PetDto> GetPetByIdAsync(string id);
        Task<PetDto> CreatePetAsync(string userId, CreatePetDto createPetDto);
        Task<PetDto> UpdatePetAsync(string id, UpdatePetDto updatePetDto);
        Task DeletePetAsync(string id);
        Task<MediaResponseDto> AddPetMediaAsync(string petId, AddMediaDto mediaDto, string baseUrl);
        Task<IEnumerable<MediaResponseDto>> GetPetMediaAsync(string petId, MediaType? type = null, string baseUrl = "");
        Task<MediaResponseDto> GetMediaByIdAsync(string mediaId, string baseUrl);
        Task DeletePetMediaAsync(string petId, string mediaId);

    }
}