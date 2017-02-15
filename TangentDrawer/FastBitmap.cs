using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TangentDrawer
{
    public unsafe sealed class FastBitmap : IDisposable
    {
        public readonly Bitmap Bitmap;
        private readonly BitmapData Data;

        public readonly int Width;
        public readonly int Height;

        public FastBitmap(Bitmap bitmap)
        {
            Bitmap = bitmap;
            Data = Bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            Width = Bitmap.Width;
            Height = Bitmap.Height;
        }

        ~FastBitmap()
        {
            Dispose();
        }

        bool disposed = false;
        public void Dispose()
        {
            if(!disposed)
                Bitmap.UnlockBits(Data);
            disposed = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int* PixelPointer(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentException("Coordinates are outside of the bitmap.");
            return (int*)((byte*)Data.Scan0 + Data.Stride * y + x * sizeof(int));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPixel(int x, int y)
        {
            return *PixelPointer(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixel(int x, int y, byte p)
        {
            *PixelPointer(x, y) = (255 << 24) | (p << 16) | (p << 8) | (p << 0);
        }
    }
}
