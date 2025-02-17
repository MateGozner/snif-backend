
using System.ComponentModel.DataAnnotations;

namespace SNIF.Core.DTOs
{
    public class ValidMediaContentTypeAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var mediaDto = (AddMediaDto)validationContext.ObjectInstance;
            var allowedImageTypes = new[] { "image/jpeg", "image/png", "image/gif" };
            var allowedVideoTypes = new[] { "video/mp4", "video/webm" };

            if (mediaDto.Type == MediaType.Photo && !allowedImageTypes.Contains(mediaDto.ContentType))
                return new ValidationResult("Invalid image type");

            if (mediaDto.Type == MediaType.Video && !allowedVideoTypes.Contains(mediaDto.ContentType))
                return new ValidationResult("Invalid video type");

            return ValidationResult.Success;
        }
    }
}