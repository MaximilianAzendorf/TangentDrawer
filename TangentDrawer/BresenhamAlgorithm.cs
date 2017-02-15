using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TangentDrawer
{
    // Author: Jason Morley 
    // Source: http://www.morleydev.co.uk/blog/2010/11/18/generic-bresenhams-line-algorithm-in-visual-basic-net/)
    //
    public static class BresenhamAlgorithm
    {
        private static void Swap<T>(ref T lhs, ref T rhs) { T temp; temp = lhs; lhs = rhs; rhs = temp; }
        
        public delegate bool PlotFunction(int x, int y);
        
        public static void Line(int x0, int y0, int x1, int y1, PlotFunction plot)
        {
            bool steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
            if (steep) { Swap<int>(ref x0, ref y0); Swap<int>(ref x1, ref y1); }
            if (x0 > x1) { Swap<int>(ref x0, ref x1); Swap<int>(ref y0, ref y1); }
            int dX = (x1 - x0), dY = Math.Abs(y1 - y0), err = (dX / 2), ystep = (y0 < y1 ? 1 : -1), y = y0;

            for (int x = x0; x <= x1; ++x)
            {
                if (!(steep ? plot(y, x) : plot(x, y))) return;
                err = err - dY;
                if (err < 0) { y += ystep; err += dX; }
            }
        }

        public static void InfiniteLine(float x0, float y0, float x1, float y1, float diagonal, PlotFunction plot)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float mx = (x1 + x0) / 2;
            float my = (y1 + y0) / 2;
            float l  = (float)Math.Sqrt(dx * dx + dy * dy);
            dx /= l;
            dy /= l;
            x0 = mx + dx * diagonal;
            y0 = my + dy * diagonal;
            x1 = mx - dx * diagonal;
            y1 = my - dy * diagonal;
            Line((int)(x0 + 0.5f), (int)(y0 + 0.5f), (int)(x1 + 0.5f), (int)(y1 + 0.5f), plot);
        }
    }
}
