using Application.Interfaces;
using QRCoder;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Infrastructure.Services
{
    public class QRCodeService : IQRCodeService
    {
        public string GenerateQRCodeBase64(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            
            byte[] darkColor = new byte[] { 0, 0, 0, 255 };
            byte[] lightColor = new byte[] { 255, 255, 255, 255 };
            byte[] qrCodeImage = qrCode.GetGraphic(20, darkColor, lightColor, false);
            return Convert.ToBase64String(qrCodeImage);
        }
    }
}
