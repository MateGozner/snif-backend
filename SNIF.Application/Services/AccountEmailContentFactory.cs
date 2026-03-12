using System.Net;
using SNIF.Core.Entities;

namespace SNIF.Application.Services
{
    internal static class AccountEmailContentFactory
    {
        private const string BrandCoral = "#F24A0D";
        private const string BrandWarmBg = "#FFF8F3";
        private const string BrandDarkText = "#2E241F";
        private const string BrandSubText = "#7B675F";

        public static AccountEmailContent CreatePasswordReset(User user, string token, string? publicBaseUrl)
        {
            var link = BuildLink(publicBaseUrl, "reset-password", user.Email, token);
            var greeting = BuildGreeting(user.Name);
            var subject = "Reset your SNIF password";

            var plainText = !string.IsNullOrWhiteSpace(link)
                ? $"{greeting},\n\nWe received a request to reset your SNIF password. Use the link below to choose a new password:\n{link}\n\nIf you didn't request this, you can ignore this email."
                : $"{greeting},\n\nWe received a request to reset your SNIF password. Use the details below in the password reset flow:\nEmail: {user.Email}\nToken: {token}\n\nIf you didn't request this, you can ignore this email.";

            var html = BuildHtmlEmail(
                greeting: HtmlEncode(greeting),
                heroEmoji: "🔑",
                headline: "Reset Your Password",
                bodyText: "We received a request to reset your SNIF password. No worries — it happens to the best of us! Click the button below to choose a new password.",
                ctaText: "Reset My Password",
                ctaLink: link,
                fallbackEmail: user.Email,
                fallbackToken: string.IsNullOrWhiteSpace(link) ? token : null,
                footnote: "If you didn't request this, you can safely ignore this email. Your password will remain unchanged."
            );

            return new AccountEmailContent("password reset", subject, plainText, html, link);
        }

        public static AccountEmailContent CreateEmailConfirmation(User user, string token, string? publicBaseUrl)
        {
            var link = BuildLink(publicBaseUrl, "confirm-email", user.Email, token);
            var greeting = BuildGreeting(user.Name);
            var subject = "Welcome to SNIF! 🐾 Verify your email";

            var plainText = !string.IsNullOrWhiteSpace(link)
                ? $"{greeting},\n\nWelcome to SNIF! Confirm your email address with the link below:\n{link}\n\nIf you didn't create this account, you can ignore this email."
                : $"{greeting},\n\nWelcome to SNIF! Use the details below in the email confirmation flow:\nEmail: {user.Email}\nToken: {token}\n\nIf you didn't create this account, you can ignore this email.";

            var html = BuildHtmlEmail(
                greeting: HtmlEncode(greeting),
                heroEmoji: "🐾",
                headline: "Welcome to SNIF!",
                bodyText: "We're so excited to have you and your furry friend join the pack! Before you start sniffing out new pals, please confirm your email address.",
                ctaText: "Verify My Email",
                ctaLink: link,
                fallbackEmail: user.Email,
                fallbackToken: string.IsNullOrWhiteSpace(link) ? token : null,
                footnote: "If you didn't create this account, you can safely ignore this email."
            );

            return new AccountEmailContent("email confirmation", subject, plainText, html, link);
        }

        private static string BuildHtmlEmail(
            string greeting,
            string heroEmoji,
            string headline,
            string bodyText,
            string ctaText,
            string? ctaLink,
            string? fallbackEmail,
            string? fallbackToken,
            string footnote)
        {
            var ctaSection = !string.IsNullOrWhiteSpace(ctaLink)
                ? $@"
                    <tr>
                      <td align=""center"" style=""padding: 8px 0 24px 0;"">
                        <a href=""{HtmlEncode(ctaLink)}"" style=""display: inline-block; background-color: {BrandCoral}; color: #ffffff; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 16px; font-weight: 600; text-decoration: none; padding: 14px 40px; border-radius: 50px; mso-padding-alt: 0;"" target=""_blank"">
                          <!--[if mso]><i style=""letter-spacing:40px;mso-font-width:-100%;mso-text-raise:21pt"">&nbsp;</i><![endif]-->
                          {HtmlEncode(ctaText)}
                          <!--[if mso]><i style=""letter-spacing:40px;mso-font-width:-100%"">&nbsp;</i><![endif]-->
                        </a>
                      </td>
                    </tr>"
                : fallbackToken != null
                ? $@"
                    <tr>
                      <td style=""padding: 8px 0 24px 0;"">
                        <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color: #f5ede8; border-radius: 12px;"">
                          <tr>
                            <td style=""padding: 16px 20px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 13px; color: {BrandDarkText};"">
                              <strong>Email:</strong> {HtmlEncode(fallbackEmail ?? string.Empty)}<br/>
                              <strong>Token:</strong> {HtmlEncode(fallbackToken)}
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>"
                : "";

            return $@"<!DOCTYPE html>
<html lang=""en"" xmlns=""http://www.w3.org/1999/xhtml"">
<head>
  <meta charset=""utf-8""/>
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
  <title>{HtmlEncode(headline)}</title>
  <!--[if mso]>
  <style>table,td {{font-family: Arial, sans-serif !important;}}</style>
  <![endif]-->
</head>
<body style=""margin: 0; padding: 0; background-color: {BrandWarmBg}; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%;"">
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color: {BrandWarmBg};"">
    <tr>
      <td align=""center"" style=""padding: 40px 16px;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""520"" style=""max-width: 520px; width: 100%;"">

          <!-- Logo -->
          <tr>
            <td align=""center"" style=""padding-bottom: 32px;"">
              <span style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 32px; font-weight: 800; color: {BrandCoral}; letter-spacing: -0.5px;"">SNIF</span>
              <span style=""font-size: 24px; vertical-align: middle; margin-left: 4px;"">🐾</span>
            </td>
          </tr>

          <!-- Card -->
          <tr>
            <td style=""background-color: #ffffff; border-radius: 24px; box-shadow: 0 4px 24px rgba(0,0,0,0.06);"">
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">

                <!-- Hero Emoji -->
                <tr>
                  <td align=""center"" style=""padding: 40px 0 8px 0;"">
                    <span style=""font-size: 48px; line-height: 1;"">{heroEmoji}</span>
                  </td>
                </tr>

                <!-- Headline -->
                <tr>
                  <td align=""center"" style=""padding: 0 40px 8px 40px;"">
                    <h1 style=""margin: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 24px; font-weight: 700; color: {BrandDarkText}; line-height: 1.3;"">
                      {HtmlEncode(headline)}
                    </h1>
                  </td>
                </tr>

                <!-- Greeting + body -->
                <tr>
                  <td style=""padding: 16px 40px 0 40px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 15px; line-height: 1.6; color: {BrandSubText};"">
                    <p style=""margin: 0 0 16px 0;"">{greeting},</p>
                    <p style=""margin: 0 0 24px 0;"">{HtmlEncode(bodyText)}</p>
                  </td>
                </tr>

                <!-- CTA -->
                {ctaSection}

                <!-- Footnote -->
                <tr>
                  <td style=""padding: 0 40px 40px 40px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 13px; line-height: 1.5; color: {BrandSubText};"">
                    <p style=""margin: 0; opacity: 0.7;"">{HtmlEncode(footnote)}</p>
                  </td>
                </tr>

              </table>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td align=""center"" style=""padding: 32px 0 0 0;"">
              <p style=""margin: 0 0 4px 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 13px; color: {BrandSubText}; opacity: 0.6;"">
                SNIF — Where pets find their perfect match
              </p>
              <p style=""margin: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 11px; color: {BrandSubText}; opacity: 0.4;"">
                You received this email because an account was created with this address.
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
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