using Net.Codecrete.QrCodeGenerator;

namespace Web.Security;

/// <summary>
/// Renders an <c>otpauth://</c> provisioning URI as an inline SVG QR code for the 2FA enrollment dialog.
/// Uses Net.Codecrete.QrCodeGenerator (no System.Drawing) so it works under Linux/containers.
/// </summary>
public static class OtpQrCode
{
    public static string ToSvg(string otpAuthUri)
    {
        var qr = QrCode.EncodeText(otpAuthUri, QrCode.Ecc.Medium);
        return qr.ToSvgString(2);
    }
}
