using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Net;
using MangaCrawlerLib;

namespace MangaCrawler
{
    public abstract class ListItem
    {
        public virtual Image GetMiniature()
        {
            return null;
        }
        public override bool Equals(object a_obj)
        {
            if (a_obj == null)
                return false;
            ListItem li = a_obj as ListItem;
            if (li == null)
                return false;
            return ID == li.ID;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }
        //public static Image ScaleImage(Image image, int maxWidth, int maxHeight)
        //{
        //    var ratioX = (double)maxWidth / image.Width;
        //    var ratioY = (double)maxHeight / image.Height;
        //    var ratio = Math.Min(ratioX, ratioY);

        //    var newWidth = (int)(image.Width * ratio);
        //    var newHeight = (int)(image.Height * ratio);

        //    var newImage = new Bitmap(newWidth, newHeight);

        //    using (var graphics = Graphics.FromImage(newImage))
        //        graphics.DrawImage(image, 0, 0, newWidth, newHeight);

        //    return newImage;
        //}

        protected void DrawItem(DrawItemEventArgs e,
            Action<Rectangle, Font> a_draw_tip)
        {
            String text = ToString();
            var img = GetMiniature();
            var imgWidth = 0;

            if (e.State.HasFlag(DrawItemState.Selected))
            {
                e.Graphics.FillRectangle(Brushes.LightBlue, e.Bounds);
            }
            else
            {
                e.DrawBackground();
            }

            if (img != null)
            {
                e.Graphics.DrawImage(img, e.Bounds.X, e.Bounds.Y, 16, 16);
                imgWidth = img.Width + 1;
            }

            var size = e.Graphics.MeasureString(text, e.Font);
            Rectangle bounds = new Rectangle(imgWidth + e.Bounds.X, e.Bounds.Y +
                (e.Bounds.Height - size.ToSize().Height) / 2,
                e.Bounds.Width, size.ToSize().Height);

            e.Graphics.DrawString(text, e.Font, Brushes.Black, bounds,
                StringFormat.GenericDefault);

            int left = (int)Math.Round(size.Width + e.Graphics.MeasureString(" ", e.Font).Width);
            Font font = new Font(e.Font.FontFamily, e.Font.Size * 9 / 10, FontStyle.Bold);
            size = e.Graphics.MeasureString("(ABGHRTW%)", font).ToSize();
            bounds = new Rectangle(imgWidth + left, e.Bounds.Y +
                (e.Bounds.Height - size.ToSize().Height) / 2 - 1,
                bounds.Width - left, bounds.Height);

            a_draw_tip(bounds, font);
        }

        public abstract void DrawItem(DrawItemEventArgs a_args);
        public abstract ulong ID { get; }
    }
}
