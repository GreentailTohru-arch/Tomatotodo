namespace Tomatotodo_Windows.Data;

public static class WindowEdgeSnap
{
    public static int Coordinate(int position, int size, int origin, int extent, int threshold)
    {
        var far = origin + Math.Max(0, extent - size);
        if (Math.Abs((long)position - origin) <= threshold) return origin;
        if (Math.Abs((long)position - far) <= threshold) return far;
        return position;
    }
}
