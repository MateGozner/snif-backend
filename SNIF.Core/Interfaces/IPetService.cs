
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
        Task<string> AddPetPhotoAsync(string id, IFormFile photo);
        Task<string> AddPetVideoAsync(string id, IFormFile video);
        Task DeletePetPhotoAsync(string id, string photoName);
        Task DeletePetVideoAsync(string id, string videoName);
    }
}