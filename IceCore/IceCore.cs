using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IceCore
{
    public class IceCore
    {

        private Random rnd = new Random(DateTime.Now.Millisecond);
        public IntPtr HWND = IntPtr.Zero;
        const uint WM_MOUSEMOVE = 0x0200;
        const uint WM_LBUTTONDOWN = 0x0201;
        const uint WM_LBUTTONUP = 0x0202;
        const uint WM_RBUTTONDOWN = 0x0204;
        const uint WM_RBUTTONUP = 0x0205;
        const uint WM_MOUSEACTIVATE = 0x0021;
        const int MK_LBUTTON = 0x0001;
        const int MK_RBUTTON = 0x0002;
        const int MK_CTRL = 0x0008;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        const uint WM_NCHITTEST = 0x0084;
        const uint WM_SETCURSOR = 0x0020;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        public const int KEYBDEVENTF_KEYDOWN = 0;
        public const int KEYBDEVENTF_KEYUP = 2;
        public const byte KEYBDEVENTF_SHIFTVIRTUAL = 0x10;
        public const byte KEYBDEVENTF_SHIFTSCANCODE = 0x2A;
        private const int WM_SETTEXT = 0x000c;
        private const uint WM_CHAR = 0x0102;

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, int Msg, int wparam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "keybd_event", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern void keybd_event(byte vk, byte scan, int flags, int extrainfo);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetCursorPos(uint x, uint y);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
            public Rectangle toRect()
            {
                return new Rectangle(Left, Top, Right - Left, Bottom - Top);
            }
        }


        /* ICECORE LIBRARY v1.0.0 - Credits to 4funsystems. */


        //Generates a rnd number between min and max, used mostly for waiting purposes.
        public int GetRandom(int min, int max)
        {
            return rnd.Next(min, max);
        }

        #region Screen Methods
        //Take a screenshot using the CopyFromScreen method and returns it as a Bitmap object.
        public Bitmap Screenshot(Rectangle r)
        {
            try
            {
                Bitmap bmpScreenshot = new Bitmap(r.Width, r.Height);
                Graphics gfxScreenshot = Graphics.FromImage(bmpScreenshot);
                gfxScreenshot.CopyFromScreen(r.X, r.Y, 0, 0, r.Size, CopyPixelOperation.SourceCopy);
                gfxScreenshot.Dispose();
                return bmpScreenshot;

            }
            catch (Exception)
            {
                return new Bitmap(1, 1);
            }

        }
        //Looks for a small bitmap onto a bigger bitmap and returns a list of coincidences as Rectangles.
        //Uses filter: Chroma color at the small image to differ background from desired image.
        //Uses filter: Banned_Color (looks for banned color components at the big image if a banned_chroma color is detected
        //             at the small image.
        public List<Rectangle> Find(Bitmap smallBmp, Bitmap bigBmp, Color chroma, Color banned, Color banned_chroma)
        {

            List<Rectangle> points = new List<Rectangle>();
            if (smallBmp != null && bigBmp != null)
            {
                BitmapData smallData =
                  smallBmp.LockBits(new Rectangle(0, 0, smallBmp.Width, smallBmp.Height),
                           System.Drawing.Imaging.ImageLockMode.ReadOnly,
                           System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                BitmapData bigData =
                  bigBmp.LockBits(new Rectangle(0, 0, bigBmp.Width, bigBmp.Height),
                           System.Drawing.Imaging.ImageLockMode.ReadOnly,
                           System.Drawing.Imaging.PixelFormat.Format24bppRgb);


                int smallStride = smallData.Stride;
                int bigStride = bigData.Stride;

                int bigWidth = bigBmp.Width;
                int bigHeight = bigBmp.Height - smallBmp.Height + 1;
                int smallWidth = smallBmp.Width * 3;
                int smallHeight = smallBmp.Height;

                Rectangle location = Rectangle.Empty;
                int margin = 0;

                unsafe
                {

                    byte* pSmall = (byte*)(void*)smallData.Scan0;
                    byte* pBig = (byte*)(void*)bigData.Scan0;

                    int smallOffset = smallStride - smallBmp.Width * 3;
                    int bigOffset = bigStride - bigBmp.Width * 3;

                    bool matchFound = true;
                    bool chroma_check = false;
                    bool banchroma_check = false;
                    bool bancolor_check = false;

                    for (int y = 0; y < bigHeight; y++)
                    {
                        for (int x = 0; x < bigWidth; x++)
                        {
                            byte* pBigBackup = pBig;
                            byte* pSmallBackup = pSmall;

                            //Look for the small picture.

                            for (int i = 0; i < smallHeight; i++)
                            {
                                int j = 0;
                                matchFound = true;
                                for (j = 0; j < smallWidth; j++)
                                {
                                    int inf = pBig[0] - margin;
                                    int sup = pBig[0] + margin;
                                    //Filter 1: Chroma colour.
                                    //As we are looking cell after cell, we should check 3 components on a row.
                                    if (pSmall[0] == chroma.B || pSmall[0] == chroma.G && chroma_check || pSmall[0] == chroma.R && chroma_check)
                                    {
                                        chroma_check = true;
                                    }
                                    else
                                    {
                                        chroma_check = false;
                                    }
                                    //Filter 2: Banned chroma colour.
                                    //As we are looking cell after cell, we should check 3 components on a row.
                                    if (pSmall[0] == banned_chroma.B || pSmall[0] == banned_chroma.G && banchroma_check || pSmall[0] == banned_chroma.R && banchroma_check)
                                    {
                                        banchroma_check = true;
                                    }
                                    else
                                    {
                                        banchroma_check = false;
                                    }
                                    //Filter 3: 
                                    //We're looking for a banned color component at the big image + positive banchroma_check.
                                    //As we are looking cell after cell we should check 3 components on a row.
                                    if ((pBig[0] == banned.B && banchroma_check) || (pBig[0] == banned.G && banchroma_check) && bancolor_check || (pBig[0] == banned.R && banchroma_check) && bancolor_check)
                                    {
                                        bancolor_check = true;
                                    }
                                    else
                                    {
                                        bancolor_check = false;
                                    }
                                    //If filter is not passed, discard the detection.
                                    if (((pSmall[0] != pBig[0]) && !chroma_check && !banchroma_check && !bancolor_check))
                                    {
                                        matchFound = false;
                                        break;
                                    }
                                    pBig++;
                                    pSmall++;
                                }

                                if (!matchFound) break;
                                //We restore the pointers.
                                pSmall = pSmallBackup;
                                pBig = pBigBackup;

                                //Next rows of the small and big pictures.
                                pSmall += smallStride * (1 + i);
                                pBig += bigStride * (1 + i);
                            }

                            //If match found, we return.
                            if (matchFound)
                            {
                                points.Add(new Rectangle(x, y, smallBmp.Width, smallBmp.Height));
                            }
                            pBig = pBigBackup;
                            pSmall = pSmallBackup;
                            pBig += 3;
                        }
                        if (y < bigHeight - 1)
                            pBig += bigOffset;
                    }
                }

                bigBmp.UnlockBits(bigData);
                smallBmp.UnlockBits(smallData);
            }
            return points;
        }
        //Looks for a small bitmap onto a bigger bitmap and returns a list of coincidences as Rectangles.
        //Uses filter: Chroma color at the small image to differ background from desired image.
        public List<Rectangle> Find(Bitmap smallBmp, Bitmap bigBmp, Color chroma)
        {

            List<Rectangle> points = new List<Rectangle>();
            if (smallBmp != null && bigBmp != null)
            {
                BitmapData smallData =
                  smallBmp.LockBits(new Rectangle(0, 0, smallBmp.Width, smallBmp.Height),
                           System.Drawing.Imaging.ImageLockMode.ReadOnly,
                           System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                BitmapData bigData =
                  bigBmp.LockBits(new Rectangle(0, 0, bigBmp.Width, bigBmp.Height),
                           System.Drawing.Imaging.ImageLockMode.ReadOnly,
                           System.Drawing.Imaging.PixelFormat.Format24bppRgb);


                int smallStride = smallData.Stride;
                int bigStride = bigData.Stride;

                int bigWidth = bigBmp.Width;
                int bigHeight = bigBmp.Height - smallBmp.Height + 1;
                int smallWidth = smallBmp.Width * 3;
                int smallHeight = smallBmp.Height;

                Rectangle location = Rectangle.Empty;
                int margin = 0;

                unsafe
                {

                    byte* pSmall = (byte*)(void*)smallData.Scan0;
                    byte* pBig = (byte*)(void*)bigData.Scan0;

                    int smallOffset = smallStride - smallBmp.Width * 3;
                    int bigOffset = bigStride - bigBmp.Width * 3;

                    bool matchFound = true;
                    bool chroma_check = false;

                    for (int y = 0; y < bigHeight; y++)
                    {
                        for (int x = 0; x < bigWidth; x++)
                        {
                            byte* pBigBackup = pBig;
                            byte* pSmallBackup = pSmall;

                            //Look for the small picture.

                            for (int i = 0; i < smallHeight; i++)
                            {
                                int j = 0;
                                matchFound = true;
                                for (j = 0; j < smallWidth; j++)
                                {
                                    int inf = pBig[0] - margin;
                                    int sup = pBig[0] + margin;
                                    //As we're looking for color cells, we should check for the 3 components in a row
                                    if (pSmall[0] == chroma.B || pSmall[0] == chroma.G && chroma_check || pSmall[0] == chroma.R && chroma_check)
                                    {
                                        chroma_check = true;
                                    }
                                    else
                                    {
                                        chroma_check = false;
                                    }
                                    //If filter is not passed, we discard the detection.
                                    if (((pSmall[0] != pBig[0]) && !chroma_check))
                                    {
                                        matchFound = false;
                                        break;
                                    }
                                    pBig++;
                                    pSmall++;
                                }

                                if (!matchFound) break;
                                //We restore the pointers.
                                pSmall = pSmallBackup;
                                pBig = pBigBackup;

                                //Next rows of the small and big pictures.
                                pSmall += smallStride * (1 + i);
                                pBig += bigStride * (1 + i);
                            }

                            //If match found, we return.
                            if (matchFound)
                            {
                                points.Add(new Rectangle(x, y, smallBmp.Width, smallBmp.Height));
                            }
                            pBig = pBigBackup;
                            pSmall = pSmallBackup;
                            pBig += 3;
                        }
                        if (y < bigHeight - 1)
                            pBig += bigOffset;
                    }
                }

                bigBmp.UnlockBits(bigData);
                smallBmp.UnlockBits(smallData);
            }
            return points;
        }
        //Looks for a small bitmap onto a bigger bitmap and returns the first coincidence as a Rectangle.
        //Uses filter: Chroma color at the small image to differ background from desired image.
        public Rectangle FindOne(Bitmap smallBmp, Bitmap bigBmp, Color chroma)
        {

            List<Rectangle> points = new List<Rectangle>();
            if (smallBmp != null && bigBmp != null)
            {
                BitmapData smallData =
                  smallBmp.LockBits(new Rectangle(0, 0, smallBmp.Width, smallBmp.Height),
                           System.Drawing.Imaging.ImageLockMode.ReadOnly,
                           System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                BitmapData bigData =
                  bigBmp.LockBits(new Rectangle(0, 0, bigBmp.Width, bigBmp.Height),
                           System.Drawing.Imaging.ImageLockMode.ReadOnly,
                           System.Drawing.Imaging.PixelFormat.Format24bppRgb);


                int smallStride = smallData.Stride;
                int bigStride = bigData.Stride;

                int bigWidth = bigBmp.Width;
                int bigHeight = bigBmp.Height - smallBmp.Height + 1;
                int smallWidth = smallBmp.Width * 3;
                int smallHeight = smallBmp.Height;

                Rectangle location = Rectangle.Empty;
                int margin = 0;

                unsafe
                {

                    byte* pSmall = (byte*)(void*)smallData.Scan0;
                    byte* pBig = (byte*)(void*)bigData.Scan0;

                    int smallOffset = smallStride - smallBmp.Width * 3;
                    int bigOffset = bigStride - bigBmp.Width * 3;

                    bool matchFound = true;
                    bool chroma_check = false;

                    for (int y = 0; y < bigHeight; y++)
                    {
                        for (int x = 0; x < bigWidth; x++)
                        {
                            byte* pBigBackup = pBig;
                            byte* pSmallBackup = pSmall;

                            //Look for the small picture.

                            for (int i = 0; i < smallHeight; i++)
                            {
                                int j = 0;
                                matchFound = true;
                                for (j = 0; j < smallWidth; j++)
                                {
                                    int inf = pBig[0] - margin;
                                    int sup = pBig[0] + margin;
                                    //As we're looking for color cells, we should check for the 3 components in a row
                                    if (pSmall[0] == chroma.B || pSmall[0] == chroma.G && chroma_check || pSmall[0] == chroma.R && chroma_check)
                                    {
                                        chroma_check = true;
                                    }
                                    else
                                    {
                                        chroma_check = false;
                                    }
                                    //If filter is not passed, we discard the detection.
                                    if (((pSmall[0] != pBig[0]) && !chroma_check))
                                    {
                                        matchFound = false;
                                        break;
                                    }
                                    pBig++;
                                    pSmall++;
                                }

                                if (!matchFound) break;
                                //We restore the pointers.
                                pSmall = pSmallBackup;
                                pBig = pBigBackup;

                                //Next rows of the small and big pictures.
                                pSmall += smallStride * (1 + i);
                                pBig += bigStride * (1 + i);
                            }

                            //If match found, we return.
                            if (matchFound)
                            {

                                bigBmp.UnlockBits(bigData);
                                smallBmp.UnlockBits(smallData);
                                return new Rectangle(x, y, smallBmp.Width, smallBmp.Height);
                            }
                            pBig = pBigBackup;
                            pSmall = pSmallBackup;
                            pBig += 3;
                        }
                        if (y < bigHeight - 1)
                            pBig += bigOffset;
                    }
                }

                bigBmp.UnlockBits(bigData);
                smallBmp.UnlockBits(smallData);
            }
            return Rectangle.Empty;
        }
        #endregion

        #region Mouse GLOBALS
        /*
         Sets the cursor on the desired point instantly.
         */
        public void SetCursor(Point point)
        {
            SetCursorPos((uint)point.X, (uint)point.Y);
        }
        /*
         Sets the cursor on the desired point following a path, with a speed.
         */
        public void Mouse_RealisticMove(Point end, int speed)
        {
            speed = 100 / speed;
            int typeX = 0, typeY = 0;
            float stagesX, stagesY;
            int distX, distY;
            while (Cursor.Position != end)
            {
                if (Cursor.Position.X <= end.X)
                {
                    distX = end.X - Cursor.Position.X;
                    typeX = 0;
                }
                else
                {
                    distX = Cursor.Position.X - end.X;
                    typeX = 1;
                }
                stagesX = distX / speed;

                if (distX < speed && distX >= 50) stagesX = 50;
                else if (distX < 50 && distX >= 25) stagesX = 25;
                else if (distX < 25 && distX >= 10) stagesX = 10;

                else if (distX < 10 && distX > 0) stagesX = 1;

                if (Cursor.Position.Y <= end.Y)
                {
                    distY = end.Y - Cursor.Position.Y;
                    typeY = 0;
                }
                else
                {
                    distY = Cursor.Position.Y - end.Y;
                    typeY = 1;
                }
                stagesY = distY / speed;

                if (distY < speed && distY >= 50) stagesY = 50;
                else if (distY < 50 && distY >= 25) stagesY = 25;
                else if (distY < 25 && distY >= 10) stagesY = 10;
                else if (distY <= 10 && distY > 0) stagesY = 1;

                if (typeX == 0)
                    SetCursor(new Point(Cursor.Position.X + (int)stagesX, Cursor.Position.Y));
                else
                    SetCursor(new Point(Cursor.Position.X - (int)stagesX, Cursor.Position.Y));

                if (typeY == 0)
                    SetCursor(new Point(Cursor.Position.X, Cursor.Position.Y + ((int)stagesY)));
                else
                    SetCursor(new Point(Cursor.Position.X, Cursor.Position.Y - ((int)stagesY)));
                Thread.Sleep(GetRandom(50, 100));
            }
        }
        /*
         Moves the cursor on the desired distance (can be negative).
         */
        public void SetCursor(int distx, int disty)
        {
            SetCursorPos((uint)(Cursor.Position.X + distx), (uint)(Cursor.Position.Y + disty));
        }
        #endregion

        #region Broadcast POINT Mouse
        //Left mouse button down at the desired point.
        public void MouseEvent_LDown(Point point)
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTDOWN, (uint)point.X, (uint)point.Y, 0, UIntPtr.Zero);
        }
        //Left mouse button up at the desired point.
        public void MouseEvent_LUp(Point point)
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTUP, (uint)point.X, (uint)point.Y, 0, UIntPtr.Zero);
        }
        //Right mouse button down at the desired point.
        public void MouseEvent_RDown(Point point)
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_RIGHTDOWN, (uint)point.X, (uint)point.Y, 0, UIntPtr.Zero);
        }
        //Right mouse button up at the desired point.
        public void MouseEvent_RUp(Point point)
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_RIGHTUP, (uint)point.X, (uint)point.Y, 0, UIntPtr.Zero);
        }
        //Left click with fast cursor movement at the desired point.
        public void MouseEvent_SlowLClick(Point point)
        {
            point = new Point(point.X, point.Y);

            Point now = Cursor.Position;
            SetCursor(point);
            MouseEvent_LDown(point);
            MouseEvent_LUp(point);
            SetCursor(now);
        }
        //Right click with fast cursor movement at the desired point.
        public void MouseEvent_SlowRClick(Point point)
        {
            Point now = Cursor.Position;
            point = new Point(point.X, point.Y);
            SetCursor(point);
            MouseEvent_RDown(point);
            MouseEvent_RUp(point);
            SetCursor(now);
        }
        //Left click with no cursor movement at the desired point.
        public void MouseEvent_FastLClick(Point point)
        {
            MouseEvent_LDown(point);
            MouseEvent_LUp(point);
        }
        //Right click with no cursor movement at the desired point.
        public void MouseEvent_FastRClick(Point point)
        {
            MouseEvent_RDown(point);
            MouseEvent_RUp(point);
        }
        //Left click with a realistic cursor movement at the desired point.
        public void MouseEvent_RealisticLClick(Point start, int speed)
        {
            Point p = Cursor.Position;
            Mouse_RealisticMove(start, speed);
            MouseEvent_LDown(start);
            MouseEvent_LUp(start);
            SetCursor(p);
        }
        //Right click with a realistic cursor movement at the desired point.
        public void MouseEvent_RealisticRClick(Point start, int speed)
        {
            Point p = Cursor.Position;
            Mouse_RealisticMove(start, speed);
            MouseEvent_RDown(start);
            MouseEvent_RUp(start);
            SetCursor(p);
        }
        //Left button drag from start point to end point with no cursor movement.
        public void MouseEvent_L_FastDrag(Point start, Point end)
        {
            MouseEvent_LDown(start);
            MouseEvent_LUp(end);
        }
        //Right button drag from start point to end point with no cursor movement.
        public void MouseEvent_R_FastDrag(Point start, Point end)
        {
            MouseEvent_RDown(start);
            MouseEvent_RUp(end);
        }
        //Left button drag from start point to end point with fast cursor movement.
        public void MouseEvent_L_SlowDrag(Point start, Point end)
        {
            Point p = Cursor.Position;
            SetCursor(start);
            MouseEvent_LDown(start);
            SetCursor(end);
            MouseEvent_LUp(end);
            SetCursor(p);
        }
        //Right button drag from start point to end point with fast cursor movement.
        public void MouseEvent_R_SlowDrag(Point start, Point end)
        {
            Point p = Cursor.Position;
            SetCursor(start);
            MouseEvent_RDown(start);
            SetCursor(end);
            MouseEvent_RUp(end);
            SetCursor(p);
        }

        //Left button drag from start point to end point with realistic cursor movement.
        public void MouseEvent_L_RealisticDrag(Point start, Point end, int speed)
        {
            Point p = Cursor.Position;
            SetCursor(start);
            MouseEvent_LDown(start);
            Mouse_RealisticMove(end, speed);
            MouseEvent_LUp(end);
            SetCursor(p);
        }
        //Right button drag from start point to end point with realistic cursor movement.
        public void MouseEvent_R_RealisticDrag(Point start, Point end, int speed)
        {
            Point p = Cursor.Position;
            SetCursor(start);
            MouseEvent_RDown(start);
            Mouse_RealisticMove(end, speed);
            MouseEvent_RUp(end);
            SetCursor(p);
        }
        #endregion

        #region Broadcast RELATIVE Mouse

        public void MouseEvent_LDown()
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTDOWN, (uint)Cursor.Position.X, (uint)Cursor.Position.Y, 0, UIntPtr.Zero);
        }
        public void MouseEvent_LUp()
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTUP, (uint)Cursor.Position.X, (uint)Cursor.Position.Y, 0, UIntPtr.Zero);
        }
        public void MouseEvent_RDown()
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_RIGHTDOWN, (uint)Cursor.Position.X, (uint)Cursor.Position.Y, 0, UIntPtr.Zero);
        }
        public void MouseEvent_RUp()
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_RIGHTUP, (uint)Cursor.Position.X, (uint)Cursor.Position.Y, 0, UIntPtr.Zero);
        }
        public void MouseEvent_LClick()
        {
            MouseEvent_LDown(Cursor.Position);
            MouseEvent_LUp(Cursor.Position);
        }
        public void MouseEvent_RClick()
        {
            MouseEvent_RDown(Cursor.Position);
            MouseEvent_RUp(Cursor.Position);
        }
        #endregion

        #region Directed POINT Mouse      
        public Rectangle GetRect()
        {
            Rect window = new Rect();
            GetWindowRect(HWND, ref window);
            return window.toRect();
        }
        public void DirMouse_LDown(Point p)
        {
            Rectangle win = GetRect();
            Point real = new Point(p.X - win.X, p.Y - win.Y);
            int lparm = (real.Y << 16) + real.X;
            SendMessage(HWND, WM_LBUTTONDOWN, 0, lparm);
            SendMessage(HWND, WM_MOUSEMOVE, 0, lparm);
        }
        public void DirMouse_LUp(Point p)
        {
            Rectangle win = GetRect();
            Point real = new Point(p.X - win.X, p.Y - win.Y);
            int lparm = (real.Y << 16) + real.X;
            SendMessage(HWND, WM_LBUTTONUP, 0, lparm);
            SendMessage(HWND, WM_MOUSEMOVE, 0, lparm);
        }
        public void DirMouse_RDown(Point p)
        {
            Rectangle win = GetRect();
            Point real = new Point(p.X - win.X, p.Y - win.Y);
            int lparm = (real.Y << 16) + real.X;
            SendMessage(HWND, WM_RBUTTONDOWN, 0, lparm);
            SendMessage(HWND, WM_MOUSEMOVE, 0, lparm);
        }
        public void DirMouse_RUp(Point p)
        {
            Rectangle win = GetRect();
            Point real = new Point(p.X - win.X, p.Y - win.Y);
            int lparm = (real.Y << 16) + real.X;
            SendMessage(HWND, WM_RBUTTONUP, 0, lparm);
            SendMessage(HWND, WM_MOUSEMOVE, 0, lparm);
        }
        public void DirMouse_RClick(Point p)
        {
            Rectangle win = GetRect();
            Point real = new Point(p.X - win.X, p.Y - win.Y);
            int lparm = (real.Y << 16) + real.X;
            SendMessage(HWND, WM_RBUTTONDOWN, 0, lparm);
            SendMessage(HWND, WM_MOUSEMOVE, 0, lparm);
            SendMessage(HWND, WM_RBUTTONUP, 0, lparm);
        }
        public void DirMouse_LClick(Point p)
        {
            Rectangle win = GetRect();
            Point real = new Point(p.X - win.X, p.Y - win.Y);
            int lparm = (real.Y << 16) + real.X;
            SendMessage(HWND, WM_LBUTTONDOWN, 0, lparm);
            SendMessage(HWND, WM_MOUSEMOVE, 0, lparm);
            SendMessage(HWND, WM_LBUTTONUP, 0, lparm);
        }
        public void DirMouse_RDrag(Point start, Point end)
        {
            DirMouse_RDown(start);
            DirMouse_RUp(end);
        }
        public void DirMouse_LDrag(Point start, Point end)
        {
            DirMouse_LDown(start);
            DirMouse_LUp(end);
        }
        #endregion

        #region Keyboard Globals
        public struct KBDBYTE
        {
            public Byte _virtual;
            public Byte _scancode;
        }
        public KBDBYTE KbdEvent_Dictionary(string c)
        {
            Byte vkey = 0x0;
            Byte scode = 0x0;

            switch (c.ToLower())
            {
                case "{ctrl}":
                    vkey = 0x11;
                    scode = 0x1D;
                    break;
                case "{alt}":
                    vkey = 0x12;
                    scode = 0x38;
                    break;
                case "{shift}":
                    vkey = 0x10;
                    scode = 0x2A;
                    break;
                case "{esc}":
                    vkey = 0x1B;
                    scode = 0x01;
                    break;
                case "{home}":
                    vkey = 0x24;
                    scode = 0x47;
                    break;
                case "{delete}":
                    vkey = 0x2E;
                    scode = 0x53;
                    break;
                case "{insert}":
                    vkey = 0x2D;
                    scode = 0x52;
                    break;
                case "{end}":
                    vkey = 0x23;
                    scode = 0x4F;
                    break;
                case "{pageup}":
                    vkey = 0x21;
                    scode = 0x49;
                    break;
                case "{pagedown}":
                    vkey = 0x22;
                    scode = 0x51;
                    break;
                case @"\":
                    vkey = 0xDC;
                    scode = 0x2B;
                    break;
                case "1":
                    vkey = 0x31;
                    scode = 0x02;
                    break;
                case "2":
                    vkey = 0x32;
                    scode = 0x03;
                    break;
                case "3":
                    vkey = 0x33;
                    scode = 0x04;
                    break;
                case "4":
                    vkey = 0x34;
                    scode = 0x05;
                    break;
                case "5":
                    vkey = 0x35;
                    scode = 0x06;
                    break;
                case "6":
                    vkey = 0x36;
                    scode = 0x07;
                    break;
                case "7":
                    vkey = 0x37;
                    scode = 0x08;
                    break;
                case "8":
                    vkey = 0x38;
                    scode = 0x09;
                    break;
                case "9":
                    vkey = 0x39;
                    scode = 0x0A;
                    break;
                case "0":
                    vkey = 0x30;
                    scode = 0x0B;
                    break;
                case "'":
                    vkey = 0xBF;
                    scode = 0x28;
                    break;
                case "{tab}":
                    vkey = 0x09;
                    scode = 0x0F;
                    break;
                case "{lockcaps}":
                    vkey = 0x14;
                    scode = 0x3A;
                    break;
                case "{win}":
                    vkey = 0x5B;
                    scode = 0x2A;
                    break;
                case "q":
                    vkey = 0x51;
                    scode = 0x10;
                    break;
                case "w":
                    vkey = 0x57;
                    scode = 0x11;
                    break;
                case "e":
                    vkey = 0x45;
                    scode = 0x12;
                    break;
                case "r":
                    vkey = 0x52;
                    scode = 0x13;
                    break;
                case "t":
                    vkey = 0x54;
                    scode = 0x14;
                    break;
                case "y":
                    vkey = 0x59;
                    scode = 0x15;
                    break;
                case "u":
                    vkey = 0x55;
                    scode = 0x16;
                    break;
                case "i":
                    vkey = 0x49;
                    scode = 0x17;
                    break;
                case "o":
                    vkey = 0x4F;
                    scode = 0x18;
                    break;
                case "p":
                    vkey = 0x50;
                    scode = 0x19;
                    break;
                case "`":
                    vkey = 0xC0;
                    scode = 0x29;
                    break;
                case "+":
                    vkey = 0xBB;
                    scode = 0x4E;
                    break;
                case "a":
                    vkey = 0x41;
                    scode = 0x1E;
                    break;
                case "s":
                    vkey = 0x53;
                    scode = 0x1F;
                    break;
                case "d":
                    vkey = 0x44;
                    scode = 0x20;
                    break;
                case "f":
                    vkey = 0x46;
                    scode = 0x21;
                    break;
                case "g":
                    vkey = 0x47;
                    scode = 0x22;
                    break;
                case "h":
                    vkey = 0x48;
                    scode = 0x23;
                    break;
                case "j":
                    vkey = 0x4A;
                    scode = 0x24;
                    break;
                case "k":
                    vkey = 0x4B;
                    scode = 0x25;
                    break;
                case "l":
                    vkey = 0x4C;
                    scode = 0x26;
                    break;
                case "´":
                    vkey = 0xDB;
                    scode = 0x1A;
                    break;
                case "ç":
                    vkey = 0xDD;
                    scode = 0x1B;
                    break;
                case "<":
                    vkey = 0xE2;
                    scode = 0x33;
                    break;
                case "z":
                    vkey = 0x5A;
                    scode = 0x4C;
                    break;
                case "x":
                    vkey = 0x58;
                    scode = 0x4D;
                    break;
                case "c":
                    vkey = 0x43;
                    scode = 0x2E;
                    break;
                case "v":
                    vkey = 0x56;
                    scode = 0x9e;
                    break;
                case "b":
                    vkey = 0x42;
                    scode = 0x30;
                    break;
                case "n":
                    vkey = 0x4E;
                    scode = 0x31;
                    break;
                case "m":
                    vkey = 0x4D;
                    scode = 0x32;
                    break;
                case ",":
                    vkey = 0xBC;
                    scode = 0x33;
                    break;
                case ".":
                    vkey = 0xBE;
                    scode = 0x34;
                    break;
                case "-":
                    vkey = 0xBD;
                    scode = 0x35;
                    break;
                case "{up}":
                    vkey = 0x26;
                    scode = 0x48;
                    break;
                case "{down}":
                    vkey = 0x28;
                    scode = 0x50;
                    break;
                case "{right}":
                    vkey = 0x27;
                    scode = 0x4D;
                    break;
                case "{left}":
                    vkey = 0x25;
                    scode = 0x4B;
                    break;
                case "{numpad_substract}":
                    vkey = 0x6D;
                    scode = 0x4A;
                    break;
                case "{numpad_add}":
                    vkey = 0x6B;
                    scode = 0x4E;
                    break;
                case "{return}":
                    vkey = 0x08;
                    scode = 0x0E;
                    break;
                case "{enter}":
                    vkey = 0x0D;
                    scode = 0x1C;
                    break;
                case " ":
                    vkey = 0x20;
                    scode = 0x39;
                    break;
                case "{numpad_dot}":
                    vkey = 0x6C;
                    scode = 0x53;
                    break;
                case "{numpad_0}":
                    vkey = 0x60;
                    scode = 0x52;
                    break;
                case "{numpad_1}":
                    vkey = 0x61;
                    scode = 0x4F;
                    break;
                case "{numpad_2}":
                    vkey = 0x62;
                    scode = 0x50;
                    break;
                case "{numpad_3}":
                    vkey = 0x63;
                    scode = 0x51;
                    break;
                case "{numpad_4}":
                    vkey = 0x64;
                    scode = 0x4B;
                    break;
                case "{numpad_5}":
                    vkey = 0x65;
                    scode = 0x4C;
                    break;
                case "{numpad_6}":
                    vkey = 0x66;
                    scode = 0x4D;
                    break;
                case "{numpad_7}":
                    vkey = 0x67;
                    scode = 0x08;
                    break;
                case "{numpad_8}":
                    vkey = 0x68;
                    scode = 0x09;
                    break;
                case "{numpad_9}":
                    vkey = 0x69;
                    scode = 0x0A;
                    break;
                case "{f1}":
                    vkey = 0x70;
                    scode = 0x3B;
                    break;
                case "{f2}":
                    vkey = 0x71;
                    scode = 0x3C;
                    break;
                case "{f3}":
                    vkey = 0x72;
                    scode = 0x3D;
                    break;
                case "{f4}":
                    vkey = 0x73;
                    scode = 0x3E;
                    break;
                case "{f5}":
                    vkey = 0x74;
                    scode = 0x3F;
                    break;
                case "{f6}":
                    vkey = 0x75;
                    scode = 0x40;
                    break;
                case "{f7}":
                    vkey = 0x76;
                    scode = 0x41;
                    break;
                case "{f8}":
                    vkey = 0x77;
                    scode = 0x42;
                    break;
                case "{f9}":
                    vkey = 0x78;
                    scode = 0x43;
                    break;
                case "{f10}":
                    vkey = 0x79;
                    scode = 0x44;
                    break;
                case "{f11}":
                    vkey = 0x7A;
                    scode = 0x85;
                    break;
                case "{f12}":
                    vkey = 0x7B;
                    scode = 0x86;
                    break;
                case "{locknum}":
                    vkey = 0x90;
                    scode = 0x45;
                    break;
                case "{scrolllock}":
                    vkey = 0x91;
                    scode = 0x46;
                    break;
                case "{print}":
                    vkey = 0x2A;
                    scode = 0x37;
                    break;
                default:
                    break;
            }
            KBDBYTE result = new KBDBYTE();
            result._virtual = vkey;
            result._scancode = scode;
            return result;
        }
        #endregion

        #region Broadcast Keyboard
        public void KeyEvent_KeyDown(string c)
        {
            KBDBYTE bte = KbdEvent_Dictionary(c);
            keybd_event(bte._virtual, bte._scancode, KEYBDEVENTF_KEYDOWN, 0);
        }
        public void KeyEvent_KeyUp(string c)
        {
            KBDBYTE bte = KbdEvent_Dictionary(c);
            keybd_event(bte._virtual, bte._scancode, KEYBDEVENTF_KEYUP, 0);
        }
        public void KeyEvent_SendKey(string key)
        {
            KeyEvent_KeyDown(key);
            KeyEvent_KeyUp(key);
        }
        public void KeySend_Send(string key)
        {
            SendKeys.Send(key);
        }
        public void KeySend_SendWait(string key)
        {
            SendKeys.SendWait(key);
        }
        #endregion

        #region Directed Keyboard.
        public void KeyMessage_KeyDown(string key)
        {
            KBDBYTE bte = KbdEvent_Dictionary(key);
            PostMessage(HWND, WM_KEYDOWN, 0, IntPtr.Zero);
        }
        public void KeyMessage_KeyUp(string key)
        {
            KBDBYTE bte = KbdEvent_Dictionary(key);
            PostMessage(HWND, WM_KEYUP, bte._virtual, IntPtr.Zero);
        }
        public void KeyMesage_Send(string key)
        {
            KeyMessage_KeyDown(key);
            KeyMessage_KeyUp(key);
        }
        #endregion

    }

}