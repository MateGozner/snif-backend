using System.Net;
using SNIF.Core.Entities;

namespace SNIF.Application.Services
{
    internal static class AccountEmailContentFactory
    {
        public static AccountEmailContent CreatePasswordReset(User user, string token, string? publicBaseUrl)
        {
            var link = BuildLink(publicBaseUrl, "reset-password", user.Email, token);
            var greeting = BuildGreeting(user.Name);
            var subject = "Reset your SNIF password";

            if (!string.IsNullOrWhiteSpace(link))
            {
                return new AccountEmailContent(
                    "password reset",
                    subject,
                    $"{greeting},\n\nWe received a request to reset your SNIF password. Use the link below to choose a new password:\n{link}\n\nIf you didn't request this, you can ignore this email.",
                    $"<p>{HtmlEncode(greeting)},</p><p>We received a request to reset your SNIF password. Use the link below to choose a new password:</p><p><a href=\"{HtmlEncode(link)}\">Reset your password</a></p><p>If you didn't request this, you can ignore this email.</p>",
                    link);
            }

            return new AccountEmailContent(
                "password reset",
                subject,
                $"{greeting},\n\nWe received a request to reset your SNIF password. Use the details below in the password reset flow:\nEmail: {user.Email}\nToken: {token}\n\nIf you didn't request this, you can ignore this email.",
                $"<p>{HtmlEncode(greeting)},</p><p>We received a request to reset your SNIF password. Use the details below in the password reset flow:</p><p>Email: <strong>{HtmlEncode(user.Email ?? string.Empty)}</strong><br/>Token: <strong>{HtmlEncode(token)}</strong></p><p>If you didn't request this, you can ignore this email.</p>",
                null);
        }

        public static AccountEmailContent CreateEmailConfirmation(User user, string token, string? publicBaseUrl)
        {
            var link = BuildLink(publicBaseUrl, "confirm-email", user.Email, token);
            var greeting = BuildGreeting(user.Name);
            var subject = "Confirm your SNIF email";

            if (!string.IsNullOrWhiteSpace(link))
            {
                return new AccountEmailContent(
                    "email confirmation",
                    subject,
                    $"{greeting},\n\nWelcome to SNIF. Confirm your email address with the link below:\n{link}\n\nIf you didn't create this account, you can ignore this email.",
                    $"<p>{HtmlEncode(greeting)},</p><p>Welcome to SNIF. Confirm your email address with the link below:</p><p><a href=\"{HtmlEncode(link)}\">Confirm your email</a></p><p>If you didn't create this account, you can ignore this email.</p>",
                    link);
            }

            return new AccountEmailContent(
                "email confirmation",
                subject,
                $"{greeting},\n\nWelcome to SNIF. Use the details below in the email confirmation flow:\nEmail: {user.Email}\nToken: {token}\n\nIf you didn't create this account, you can ignore this email.",
                $"<p>{HtmlEncode(greeting)},</p><p>Welcome to SNIF. Use the details below in the email confirmation flow:</p><p>Email: <strong>{HtmlEncode(user.Email ?? string.Empty)}</strong><br/>Token: <strong>{HtmlEncode(token)}</strong></p><p>If you didn't create this account, you can ignore this email.</p>",
                null);
        }

        public static string? BuildLink(string? publicBaseUrl, string route, string? email, string token)
        {
            if (string.IsNullOrWhiteSpace(publicBaseUrl) || string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return $"{publicBaseUrl.TrimEnd('/')}/{route}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        }

        private static string BuildGreeting(string? name)
        {
            return string.IsNullOrWhiteSpace(name) ? "Hello" : $"Hello {name}";
        }

        private static string HtmlEncode(string value)
        {
            return WebUtility.HtmlEncode(value);
        }
    }

    internal sealed record AccountEmailContent(
        string Action,
        string Subject,
        string PlainTextBody,
        string HtmlBody,
        string? Link);
}