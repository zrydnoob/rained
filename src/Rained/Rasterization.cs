namespace Rained;
using System.Diagnostics;

static class Rasterization
{
    // adapted from the psuedocode on the Wikipedia page
    public static void Bresenham(int x0, int y0, int x1, int y1, Action<int, int> plot)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            plot(x0, y0);
            if (x1 == x0 && y0 == y1) break;
            int e2 = 2 * error;
            
            if (e2 >= dy)
            {
                if (x0 == x1) break;
                error += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                if (y0 == y1) break;
                error += dx;
                y0 += sy;
            }
        }
    }

    public static void Line(float x0, float y0, float x1, float y1, Action<int, int> plot)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        if (dx == 0 && dy == 0)
        {
            plot((int)Math.Floor(x0), (int)Math.Floor(y0));
            return;
        }

        var lineLen = MathF.Sqrt(dx*dx + dy*dy);
        dx /= lineLen;
        dy /= lineLen;

        int stepX = 0;   
        if (x1 > x0)
            stepX = 1;
        else if (x1 < x0)
            stepX = -1;
        
        int stepY = 0;        
        if (y1 > y0)
            stepY = 1;
        else if (y1 < y0)
            stepY = -1;
        
        int ix = (int)Math.Floor(x0);
        int iy = (int)Math.Floor(y0);
        float x = x0;
        float y = y0;
        float traveled = 0f;

        int endIx = (int)Math.Floor(x1);
        int endIy = (int)Math.Floor(y1);

        while (true)
        {
            plot(ix, iy);
            if (ix == endIx && iy == endIy) break;
            Debug.Assert(traveled < lineLen + 1f);

            float distX = float.PositiveInfinity;
            if (stepX > 0)
                distX = ix + 1 - x;
            else if (stepX < 0)
                distX = ix - x;
            
            float distY = float.PositiveInfinity;
            if (stepY > 0)
                distY = iy + 1 - y;
            else if (stepY < 0)
                distY = iy - y;

            float hypX = distX / dx;
            float hypY = distY / dy;

            if (hypX == hypY)
            {
                ix += stepX;
                iy += stepY;
                x += dx * hypX;
                y += dy * hypX;
                traveled += hypX;

            }
            else if (hypX <= hypY)
            {
                ix += stepX;
                x += dx * hypX;
                y += dy * hypX;
                traveled += hypX;
            }
            else
            {
                iy += stepY;
                y += dy * hypY;
                x += dx * hypY;
                traveled += hypY;
            }
        }
    }

    public static bool FloodFill(int srcX, int srcY, int mapWidth, int mapHeight, Func<int, int, bool> isSimilar, Action<int, int> plot)
    {
        bool IsInBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < mapWidth && y < mapHeight;
        }
        if (!IsInBounds(srcX, srcY)) return true;

        // use a recursive scanline fill algorithm
        // with a manually-managed stack
        Stack<(int, int)> fillStack = [];
        fillStack.Push((srcX, srcY));

        while (fillStack.Count > 0)
        {
            if (fillStack.Count > 100000)
            {
                Log.UserLogger.Error("Flood fill stack overflow!");
                return false;
            }

            (int x, int y) = fillStack.Pop();

            // go to left bounds of this scanline
            while (IsInBounds(x, y) && isSimilar(x, y))
                x--;

            x++;

            bool oldAboveEmpty = false;
            bool oldBelowEmpty = false;

            // go to right bounds of the scanline, spawning new scanlines above or below if detected
            while (IsInBounds(x, y) && isSimilar(x, y))
            {
                bool aboveEmpty = IsInBounds(x, y-1) && isSimilar(x, y-1);
                bool belowEmpty = IsInBounds(x, y+1) && isSimilar(x, y+1);

                if (aboveEmpty != oldAboveEmpty && aboveEmpty)
                {
                    fillStack.Push((x, y-1));
                }

                if (belowEmpty != oldBelowEmpty && belowEmpty)
                {
                    fillStack.Push((x, y+1));
                }

                oldAboveEmpty = aboveEmpty;
                oldBelowEmpty = belowEmpty;

                plot(x, y);
                x++;
            } 
        }

        return true;
    }
}