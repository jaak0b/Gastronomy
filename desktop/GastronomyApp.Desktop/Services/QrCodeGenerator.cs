using System.Collections;
using QRCoder;

namespace GastronomyApp.Desktop.Services;

public sealed class QrCodeGenerator : IQrCodeGenerator
{
    public IReadOnlyList<bool[]> GenerateMatrix(string content)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);

        List<bool[]> matrix = [];

        foreach (BitArray row in data.ModuleMatrix)
        {
            bool[] modules = new bool[row.Length];
            row.CopyTo(modules, 0);
            matrix.Add(modules);
        }

        return matrix;
    }
}
