using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;

//namespace para manipulação de imagens

namespace JqueryFileUpload
{
    /// <summary>
    /// Summary description for Upload
    /// </summary>
    public class Upload : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/plain";//"application/json";
            var r = new System.Collections.Generic.List<ViewDataUploadFilesResult>();
            JavaScriptSerializer js = new JavaScriptSerializer();
            foreach (string file in context.Request.Files)
            {
                HttpPostedFile hpf = context.Request.Files[file] as HttpPostedFile;
                string FileName = string.Empty;
                if (HttpContext.Current.Request.Browser.Browser.ToUpper() == "IE")
                {
                    string[] files = hpf.FileName.Split(new char[] { '\\' });
                    FileName = files[files.Length - 1];
                }
                else
                {
                    FileName = hpf.FileName;
                }
                if (hpf.ContentLength == 0)
                    continue;

                // Carrega stream da imagem do buffer e pega alguns atributos
                Stream imgStream = context.Request.Files[file].InputStream;

                // Define o nome da imagem a ser gravada
                var imgName = "__" + FileName;

                // Localização onde a imagem vai ser salva (relativo a aplicação)
                var imgPath = "~/imagens";
                var imgSaveLocation = context.Server.MapPath(imgPath) + "\\" + imgName;

                // Tamanho da imagem final
                int imgHeight = 50;
                int imgWidth = 50;

                Bitmap newFile = ResizeImage(imgStream, imgWidth, imgHeight);
                newFile.Save(imgSaveLocation, System.Drawing.Imaging.ImageFormat.Jpeg);

                r.Add(new ViewDataUploadFilesResult()
                {
                    thumbnail_url = VirtualPathUtility.ToAbsolute(imgPath) +"/"+ imgName,
                    name = imgName,
                    length = hpf.ContentLength,
                    type = hpf.ContentType
                });
                var uploadedFiles = new
                {
                    files = r.ToArray()
                };
                var jsonObj = js.Serialize(uploadedFiles);
                //jsonObj.ContentEncoding = System.Text.Encoding.UTF8;
                //jsonObj.ContentType = "application/json;";
                context.Response.Write(jsonObj.ToString());
            }
        }

        // Redimensionamento da imagem
        public Bitmap ResizeImage(Stream imgStream, int? newWidth, int? newHeight)
        {
            Bitmap imgOut = null;

            // fallback dos tamanhos das imagens
            if (newHeight == null)
                newHeight = 800;
            if (newWidth == null)
                newWidth = 600;

            try
            {
                Bitmap loBMP = new Bitmap(imgStream);
                ImageFormat loFormat = loBMP.RawFormat;

                //*** Se a imagem for menor que a nova retorna a nova
                if (loBMP.Width < newWidth && loBMP.Height < newHeight)
                {
                    return loBMP;
                }

                imgOut = new Bitmap((int)newWidth, (int)newHeight);
                Graphics g = Graphics.FromImage(imgOut);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.FillRectangle(Brushes.White, 0, 0, (int)newWidth, (int)newHeight);
                g.DrawImage(loBMP, 0, 0, (int)newWidth, (int)newHeight);
                loBMP.Dispose();
            }
            catch (Exception)
            {
                return null;
            }

            return imgOut;
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }

    public class ViewDataUploadFilesResult
    {
        public string thumbnail_url { get; set; }
        public string name { get; set; }
        public int length { get; set; }
        public string type { get; set; }
    }
}
