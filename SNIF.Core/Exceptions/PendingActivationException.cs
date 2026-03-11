using SNIF.Core.DTOs;

namespace SNIF.Core.Exceptions
{
    public class PendingActivationException : Exception
    {
        public PendingActivationException(AuthResponseDto response)
            : base(response.Message ?? "Email confirmation is required before sign in.")
        {
            Response = response;
        }

        public AuthResponseDto Response { get; }
    }
}