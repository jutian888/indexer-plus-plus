// The code in this file with some changes originates from Stack Overflow
// posted on http://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows
// and was written by Daniel Pe�alba http://stackoverflow.com/users/402081/daniel-pe%C3%B1alba

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using CLIInterop;

namespace Indexer
{
    [Flags]
    public enum ThumbnailOptions
    {
        ResizeToFit = 0x00,
        BiggerSizeOk = 0x01,
        InMemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10
    }

    public class ThumbnailProvider : IThumbnailProvider
    {
        #region shell32 dlls and related data structutres importing

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            // The following parameter is not used - binding context.
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);


        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        internal interface IShellItem
        {
            void BindToHandler(IntPtr pbc,
                [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
                [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                out IntPtr ppv);

            void GetParent(out IShellItem ppsi);
            void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }


        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItemImageFactory
        {
            [PreserveSig]
            HResult GetImage(
                [In, MarshalAs(UnmanagedType.Struct)] NativeSize size,
                [In] ThumbnailOptions flags,
                [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeSize
        {
            private int width;
            private int height;

            public int Width
            {
                set { width = value; }
            }

            public int Height
            {
                set { height = value; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rgbquad
        {
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
            public byte rgbReserved;
        }

        #endregion

        private ThumbnailProvider()
        {
        }

        private static ThumbnailProvider instance;

        public static ThumbnailProvider Instance
        {
            get { return instance ?? (instance = new ThumbnailProvider()); }
        }

        public IconSizeEnum ThumbSize = IconSizeEnum.SmallIcon16;

        private int GetThumbSizePixels()
        {
            switch (ThumbSize)
            {
                case IconSizeEnum.SmallIcon16:
                    return 16;
                case IconSizeEnum.MediumIcon32:
                    return 32;
                case IconSizeEnum.LargeIcon48:
                    return 48;
                case IconSizeEnum.JumboIcon256:
                    return 256;
                default:
                    return 32;
            }
        }

        public BitmapSource GetThumbnail(string fileName)
        {
            var thumbSizePixels = GetThumbSizePixels();
            var bitmapHandle = IntPtr.Zero;
            Bitmap bmpThumbnail = null;
            const ThumbnailOptions options = ThumbnailOptions.BiggerSizeOk | ThumbnailOptions.ResizeToFit;

            try
            {
                bitmapHandle = GetBitmapHandle(Path.GetFullPath(fileName), thumbSizePixels, options);
                bmpThumbnail = GetBitmapFromHandle(bitmapHandle);
            }
            catch (Exception e)
            {
                Log.Instance.Error("Cannot retrieve thumbnail " + e.Message);
            }
            finally
            {
                // Delete HBitmap to avoid memory leaks.
                WinApiFunctions.DeleteObject(bitmapHandle);
            }

            return bmpThumbnail == null ? null : bmpThumbnail.ToBitmapSource();
        }

        private static Bitmap GetBitmapFromHandle(IntPtr nativeHBitmap)
        {
            var bmp = Image.FromHbitmap(nativeHBitmap);

            if (Image.GetPixelFormatSize(bmp.PixelFormat) < 32)
                return bmp;

            return CreateAlphaBitmap(bmp, PixelFormat.Format32bppPArgb);
        }

        private static Bitmap CreateAlphaBitmap(Bitmap srcBitmap, PixelFormat targetPixelFormat)
        {
            var result = new Bitmap(srcBitmap.Width, srcBitmap.Height, targetPixelFormat);

            var bmpBounds = new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height);

            var srcData = srcBitmap.LockBits(bmpBounds, ImageLockMode.ReadOnly, srcBitmap.PixelFormat);

            var isAlplaBitmap = false;

            try
            {
                for (var y = 0; y <= srcData.Height - 1; y++)
                {
                    for (var x = 0; x <= srcData.Width - 1; x++)
                    {
                        var pixelColor = Color.FromArgb(Marshal.ReadInt32(srcData.Scan0, srcData.Stride*y + 4*x));

                        if (pixelColor.A > 0 & pixelColor.A < 255)
                        {
                            isAlplaBitmap = true;
                        }

                        result.SetPixel(x, y, pixelColor);
                    }
                }
            }
            finally
            {
                srcBitmap.UnlockBits(srcData);
            }

            return isAlplaBitmap ? result : srcBitmap;
        }

        private const string ShellItem2Guid = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";

        private static IntPtr GetBitmapHandle(string fileName, int size, ThumbnailOptions options)
        {
            IShellItem nativeShellItem;
            var shellItem2Guid = new Guid(ShellItem2Guid);
            var retCode = SHCreateItemFromParsingName(fileName, IntPtr.Zero, ref shellItem2Guid, out nativeShellItem);

            if (retCode != 0) throw Marshal.GetExceptionForHR(retCode);

            var nativeSize = new NativeSize
            {
                Width = size,
                Height = size
            };

            IntPtr hBitmap;
            var hr = ((IShellItemImageFactory) nativeShellItem).GetImage(nativeSize, options, out hBitmap);

            Marshal.ReleaseComObject(nativeShellItem);

            if (hr == HResult.Ok) return hBitmap;

            throw Marshal.GetExceptionForHR((int) hr);
        }
    }
}