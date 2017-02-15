using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TangentDrawer
{
    class Renderer
    {
        private readonly Program.Args args;
        private readonly Func<float, Tuple<float, float>> f;
        private readonly Func<float, Tuple<float, float>> b;
        private readonly double[,] renderTarget;
        private Task renderingTask;
        private CancellationTokenSource cToken;
        
        private long lineCount = 0;
        public long LineCount => lineCount;

        public Renderer(Program.Args args, Func<float, Tuple<float, float>> func, Func<float, Tuple<float,float>> brightnessFunction)
        {
            this.args = args;
            f = func;
            b = brightnessFunction;
            renderTarget = new double[args.Width, args.Height];
            cToken = new CancellationTokenSource();
        }

        public void Start()
        {
            Stop();

            ClearRenderTarget();
            CancellationToken token = cToken.Token;
            renderingTask = new Task(() => Worker(token), token);
            renderingTask.Start();
        }

        public void Stop()
        {
            if (renderingTask != null)
            {
                cToken.Cancel();
                renderingTask.Wait();
                renderingTask = null;
                cToken = null;
            }
        }

        public void Wait()
        {
            renderingTask.Wait();
        }

        public bool IsRunning()
        {
            return renderingTask != null && !renderingTask.IsCompleted;
        }

        public Bitmap StopAndFinalize()
        {
            Stop();

            double max = 0;

            for (int i = 0; i < renderTarget.GetLength(0); i++)
            {
                for (int j = 0; j < renderTarget.GetLength(1); j++)
                {
                    max = Math.Max(max, renderTarget[i, j]);
                }
            }

            Bitmap bmp = new Bitmap(renderTarget.GetLength(0), renderTarget.GetLength(1), PixelFormat.Format32bppArgb);
            bmp.Save("output.png");
            using (FastBitmap fbmp = new FastBitmap(bmp))
            {
                for(int i = 0; i < renderTarget.GetLength(0); i++)
                {
                    for(int j = 0; j < renderTarget.GetLength(1); j++)
                    {
                        byte p = (byte)(Math.Pow((1 - renderTarget[i, j] / (double)max), 3f) * byte.MaxValue + 0.5);
                        fbmp.SetPixel(i, j, p);
                    }
                }
            }

            return bmp;
        }

        public void ClearRenderTarget()
        {
            if(renderingTask != null)
            {
                throw new InvalidOperationException("The worker is still running.");
            }
            for(int i = 0; i < renderTarget.GetLength(0); i++)
            {
                for(int j = 0; j < renderTarget.GetLength(1); j++)
                {
                    renderTarget[i, j] = 0;
                }
            }
        }

        private bool bounded(int x, int min, int max)
        {
            return x >= min && x < max;
        }

        public void Worker(CancellationToken token)
        {
            lineCount = 0;

            float frameOffX = (args.Width - args.FrameWidth) / 2f;
            float frameOffY = (args.Height - args.FrameHeight) / 2f;
            float diagonal = 2*(float)Math.Sqrt(args.Width * args.Width + args.Height + args.Height);
            float D = (float)args.DerivativeOffset;

            Task[] tasks = new Task[Environment.ProcessorCount - 1];
            double[][,] localRenderTargets = new double[tasks.Length][,];
            for (int tid = 0; tid < tasks.Length; tid++)
            {
                int _tid = tid;
                localRenderTargets[tid] = new double[renderTarget.GetLength(0), renderTarget.GetLength(1)];
                tasks[tid] = new Task(() =>
                {
                    Random rnd = new Random((int)(DateTime.Now.Ticks * (10 + 3 * _tid)));
                    double[,] lrenderTarget = localRenderTargets[_tid];

                    while (!token.IsCancellationRequested && (args.MaxIterations < 0 || LineCount < args.MaxIterations))
                    {
                        float t;
                        if (args.Parametric)
                        {
                            t = args.TMin + D/2 + (float)rnd.NextDouble() * (args.TMax - args.TMin - D);
                        }
                        else
                        {
                            t = args.XMin + D/2 + (float)rnd.NextDouble() * (args.XMax - args.XMin - D);
                        }
                        var t0 = f(t - D / 2);
                        var t1 = f(t + D / 2);
                        float x0 = t0.Item1;
                        float x1 = t1.Item1;
                        float y0 = t0.Item2;
                        float y1 = t1.Item2;

                        if (args.OmitSteepTangents && Math.Abs((y0 - y1) / (x0 - x1)) > 30f)
                            continue;

                        float dx0 = (x0 - args.XMin) / (args.XMax - args.XMin) * args.FrameWidth + frameOffX;
                        float dx1 = (x1 - args.XMin) / (args.XMax - args.XMin) * args.FrameWidth + frameOffX;
                        float dy0 = (y0 - args.YMin) / (args.YMax - args.YMin) * args.FrameHeight + frameOffY;
                        float dy1 = (y1 - args.YMin) / (args.YMax - args.YMin) * args.FrameHeight + frameOffY;
                        
                        BresenhamAlgorithm.InfiniteLine(dx0, dy0, dx1, dy1, diagonal, (x, y) =>
                        {
                            if (bounded(x, 0, lrenderTarget.GetLength(0)) && bounded(y, 0, lrenderTarget.GetLength(1)))
                                lrenderTarget[x, y] += b(t).Item2;
                            return true;
                        });
                        Interlocked.Increment(ref lineCount);
                    }
                });
                tasks[tid].Start();
            }

            Task.WaitAll(tasks);
            for (int i = 0; i < renderTarget.GetLength(0); i++)
            {
                for(int j = 0; j < renderTarget.GetLength(1); j++)
                {
                    for(int tid = 0; tid < tasks.Length; tid++)
                    {
                        renderTarget[i, j] += localRenderTargets[tid][i, j];
                    }
                }
            }
        }
    }
}
