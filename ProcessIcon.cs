using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string inputPath = @"C:\Users\Shawn\.gemini\antigravity-ide\brain\60ba87f9-9d98-4438-869c-9026cae57f32\fox_wifi_transparent_1786955571610.png";
        string outputPath = @"icon.ico";
        
        using (Bitmap origBmp = new Bitmap(inputPath))
        {
            // MUST use 32bppArgb to store transparency
            using (Bitmap bmp = new Bitmap(origBmp.Width, origBmp.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(origBmp, 0, 0);
                }

                // Scan ALL pixels and make near-white transparent
                for (int y = 0; y < bmp.Height; y++)
                {
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        // Very relaxed white check to catch all background noise
                        if (c.R > 230 && c.G > 230 && c.B > 230)
                        {
                            // If it's pure white or very close, make fully transparent
                            if (c.R > 245 && c.G > 245 && c.B > 245)
                            {
                                bmp.SetPixel(x, y, Color.Transparent);
                            }
                            else
                            {
                                // Semi-transparent for anti-aliasing edges
                                int alpha = (255 - c.R) * 5;
                                if (alpha > 255) alpha = 255;
                                if (alpha < 0) alpha = 0;
                                bmp.SetPixel(x, y, Color.FromArgb(alpha, c.R, c.G, c.B));
                            }
                        }
                    }
                }

                // Save as ICO format
                using (FileStream fs = new FileStream(outputPath, FileMode.Create))
                {
                    BinaryWriter bw = new BinaryWriter(fs);
                    bw.Write((short)0);
                    bw.Write((short)1);
                    bw.Write((short)1);
                    
                    int w = bmp.Width;
                    int h = bmp.Height;
                    bw.Write((byte)(w >= 256 ? 0 : w));
                    bw.Write((byte)(h >= 256 ? 0 : h));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((short)1);
                    bw.Write((short)32);
                    
                    // Save PNG into a memory stream
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        byte[] pngData = ms.ToArray();
                        bw.Write(pngData.Length);
                        bw.Write(22); // offset
                        bw.Write(pngData);
                    }
                }
            }
        }
    }

    static void javaStyleFloodFill(Bitmap bmp, bool[,] visited, int startX, int startY)
    {
        // Actually let's just make everything near the edges transparent
        // to guarantee the background is removed, even if there's a 1px border.
        var q = new System.Collections.Generic.Queue<Point>();
        
        // Scan the first 5 pixels of the borders to find a valid white pixel to start flood fill
        for (int i = 0; i < 5; i++)
        {
            if (startX + i < bmp.Width) q.Enqueue(new Point(startX + i, startY));
            if (startX - i >= 0) q.Enqueue(new Point(startX - i, startY));
            if (startY + i < bmp.Height) q.Enqueue(new Point(startX, startY + i));
            if (startY - i >= 0) q.Enqueue(new Point(startX, startY - i));
        }

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            if (p.X < 0 || p.X >= bmp.Width || p.Y < 0 || p.Y >= bmp.Height) continue;
            if (visited[p.X, p.Y]) continue;
            
            Color c = bmp.GetPixel(p.X, p.Y);
            // Relaxed white check for AI generated images
            if (c.R > 200 && c.G > 200 && c.B > 200)
            {
                visited[p.X, p.Y] = true;
                q.Enqueue(new Point(p.X + 1, p.Y));
                q.Enqueue(new Point(p.X - 1, p.Y));
                q.Enqueue(new Point(p.X, p.Y + 1));
                q.Enqueue(new Point(p.X, p.Y - 1));
            }
        }
    }
}
