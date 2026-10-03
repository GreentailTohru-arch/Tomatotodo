using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Tomatotodo_Windows.Accounts;

public sealed class WindowsImageProcessingService : IImageProcessingService
{
    public async Task<byte[]> ProcessAvatarAsync(AvatarSelection selection, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(selection.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png") || selection.Bytes.Length > 20 * 1024 * 1024)
            throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u9009\u62E9\u4E0D\u8D85\u8FC7 20 MB \u7684 JPEG\u3001JPG \u6216 PNG \u56FE\u7247\u3002"));
        using var input = new MemoryStream(selection.Bytes);
        using var stream = input.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        if (decoder.DecoderInformation.CodecId != BitmapDecoder.JpegDecoderId && decoder.DecoderInformation.CodecId != BitmapDecoder.PngDecoderId)
            throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u56FE\u7247\u5185\u5BB9\u5FC5\u987B\u4E3A JPEG \u6216 PNG \u683C\u5F0F\u3002"));
        if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0 || decoder.PixelWidth > 32768 || decoder.PixelHeight > 32768 ||
            (ulong)decoder.PixelWidth * decoder.PixelHeight > 80_000_000)
            throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u56FE\u7247\u5C3A\u5BF8\u8FC7\u5927\uFF0C\u8BF7\u5148\u7F29\u5C0F\u56FE\u7247\u3002"));
        // Scale before EXIF rotation, crop afterwards in the oriented coordinate space.
        var scale = 256d / Math.Min(decoder.OrientedPixelWidth, decoder.OrientedPixelHeight);
        var orientedWidth = (uint)Math.Ceiling(decoder.OrientedPixelWidth * scale);
        var orientedHeight = (uint)Math.Ceiling(decoder.OrientedPixelHeight * scale);
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Ceiling(decoder.PixelWidth * scale),
            ScaledHeight = (uint)Math.Ceiling(decoder.PixelHeight * scale),
            InterpolationMode = BitmapInterpolationMode.Fant,
            Bounds = new BitmapBounds { X = (orientedWidth - 256) / 2, Y = (orientedHeight - 256) / 2, Width = 256, Height = 256 }
        };
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
        cancellationToken.ThrowIfCancellationRequested();
        return await EncodeAsync(pixels.DetachPixelData());
    }

    public Task<byte[]> CreateDefaultAvatarAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pixels = new byte[256 * 256 * 4];
        for (var y = 0; y < 256; y++)
            for (var x = 0; x < 256; x++)
            {
                var head = Math.Pow(x - 128, 2) + Math.Pow(y - 90, 2) < 35 * 35;
                var body = y > 143 && Math.Pow((x - 128) / 76d, 2) + Math.Pow((y - 223) / 75d, 2) < 1;
                var index = (y * 256 + x) * 4;
                pixels[index] = pixels[index + 1] = pixels[index + 2] = (byte)(head || body ? 230 : 88);
                pixels[index + 3] = 255;
            }
        return EncodeAsync(pixels);
    }

    private static async Task<byte[]> EncodeAsync(byte[] pixels)
    {
        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, 256, 256, 96, 96, pixels);
        await encoder.FlushAsync();
        output.Seek(0);
        using var reader = new DataReader(output);
        await reader.LoadAsync((uint)output.Size);
        var result = new byte[(int)output.Size]; reader.ReadBytes(result);
        return result;
    }
}
