namespace Tomatotodo_Windows.Accounts;

// UI-independent crop coordinates, shared by the preview and PNG export.
public sealed class AvatarCrop(byte[] pixels, int width, int height)
{
    public byte[] Pixels { get; private set; } = pixels;
    public int Width { get; private set; } = width;
    public int Height { get; private set; } = height;
    public double Zoom { get; private set; } = 1;
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Side => Math.Min(Width, Height) / Zoom;
    public double Left => (Width - Side) / 2 - X;
    public double Top => (Height - Side) / 2 - Y;

    public void SetZoom(double zoom)
    {
        Zoom = Math.Clamp(zoom, 1, 4);
        Move(0, 0);
    }
    public void Move(double dx, double dy)
    {
        X = Math.Clamp(X + dx, -(Width - Side) / 2, (Width - Side) / 2);
        Y = Math.Clamp(Y + dy, -(Height - Side) / 2, (Height - Side) / 2);
    }
    public void Center() { X = Y = 0; SetZoom(1); }
    public void Rotate()
    {
        var rotated = new byte[Pixels.Length];
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                Buffer.BlockCopy(Pixels, (y * Width + x) * 4, rotated, (x * Height + Height - 1 - y) * 4, 4);
        Pixels = rotated;
        (Width, Height) = (Height, Width);
        (X, Y) = (-Y, X);
        Move(0, 0);
    }
    public byte[] Export(int size = 256)
    {
        var result = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var sx = Math.Clamp(Left + (x + .5) * Side / size - .5, 0, Width - 1);
                var sy = Math.Clamp(Top + (y + .5) * Side / size - .5, 0, Height - 1);
                var x0 = (int)sx; var y0 = (int)sy;
                var x1 = Math.Min(x0 + 1, Width - 1); var y1 = Math.Min(y0 + 1, Height - 1);
                var fx = sx - x0; var fy = sy - y0;
                for (var c = 0; c < 4; c++)
                {
                    var top = Pixels[(y0 * Width + x0) * 4 + c] * (1 - fx) + Pixels[(y0 * Width + x1) * 4 + c] * fx;
                    var bottom = Pixels[(y1 * Width + x0) * 4 + c] * (1 - fx) + Pixels[(y1 * Width + x1) * 4 + c] * fx;
                    result[(y * size + x) * 4 + c] = (byte)Math.Round(top * (1 - fy) + bottom * fy);
                }
            }
        return result;
    }
}
