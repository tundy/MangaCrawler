using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using TomanuExtensions;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls;
using Image = System.Drawing.Image;

namespace MangaCrawlerLib
{
    public abstract class Entity
    {
        public ulong ID { get; internal set; }
        public ulong LimiterOrder;
        public string URL { get; internal set; }

        public Image Miniature { get; private set; }

        private static Image ScaleImage(Image image, int maxWidth, int maxHeight)
        {
            if (image == null) return null;
            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);

            var newImage = new Bitmap(newWidth, newHeight);

            using (var graphics = Graphics.FromImage(newImage))
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);

            return newImage;
        }

        public virtual void UpdateMiniatureViaCrawler()
        {
            throw new NotImplementedException();
        }

        internal virtual void SetMiniature(string uri, int maxWidth, int maxHeight)
        {
            SetMiniature(uri);
            Miniature = ScaleImage(Miniature, maxWidth, maxHeight);
        }

        internal virtual void SetMiniature(Bitmap bmp, int maxWidth, int maxHeight)
        {
            Miniature = ScaleImage(bmp, maxWidth, maxHeight);
        }
        internal virtual void SetMiniature(Bitmap bmp)
        {
            Miniature = bmp;
        }
        internal virtual void SetMiniature(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                Miniature = new Bitmap(8,8);
                return;
            }
            Uri uriTest;
            var wc = new WebClient();
            var ur = Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out uriTest) ? uri : string.Empty;
            if(!string.IsNullOrEmpty(ur))
                Miniature = Image.FromStream(wc.OpenRead(ur));
        }

        protected Entity(ulong a_id)
        {
            ID = a_id;
        }

        public override bool Equals(object a_obj)
        {
            if (a_obj == null)
                return false;
            Entity entity = a_obj as Entity;
            if (Object.ReferenceEquals(entity, null))
                return false;
            return this == entity;
        }

        public static bool operator ==(Entity a_left, Entity a_right)
        {
            if (Object.ReferenceEquals(a_left, a_right))
                return true;
            if (Object.ReferenceEquals(a_left, null))
                return false;
            if (Object.ReferenceEquals(a_right, null))
                return false;
            return a_left.ID == a_right.ID;
        }

        public static bool operator !=(Entity a_left, Entity a_right)
        {
            return !(a_left == a_right);
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        public bool IsDirectoryExists()
        {
            try
            {
                return new DirectoryInfo(GetDirectory()).Exists;
            }
            catch
            {
                return false;
            }
        }

        public static string HtmlDecode(string a_str)
        {
            a_str = HttpUtility.HtmlDecode(a_str);
            return Uri.UnescapeDataString(a_str);
        }

        internal abstract Crawler Crawler { get; }
        public abstract bool IsDownloading { get; }
        public abstract string GetDirectory();
    }
}
